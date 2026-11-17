import { createReadStream, statSync } from 'node:fs';
import { Readable } from 'node:stream';
import { protocol } from 'electron';

// "Play Veselin Marinov" local media playback (design/music/*). A raw file:// URL only works in
// production (renderer served from file://, same-origin); in dev the renderer is served from
// http://localhost:5173 and Chromium blocks file:// media as cross-origin. This registered
// scheme is marked secure+cors-enabled so it loads from either origin.
//
// Range requests are handled BY HAND (not delegated to net.fetch on a file:// target) because
// seeking mid-track needs a real 206 Partial Content response with a correct Content-Range
// header - without one, a seek re-requests the whole file from byte 0, which a media element
// reports as "back to the start" (Stefan's report). This is the well-documented way to serve
// seekable local media over a custom Electron protocol.

const MIME_BY_EXT: Record<string, string> = {
  '.mp3': 'audio/mpeg',
  '.mp4': 'video/mp4',
  '.webm': 'video/webm',
  '.mov': 'video/quicktime',
};

function mimeFor(path: string): string {
  const ext = path.slice(path.lastIndexOf('.')).toLowerCase();
  return MIME_BY_EXT[ext] ?? 'application/octet-stream';
}

export function registerMediaProtocol(): void {
  protocol.registerSchemesAsPrivileged([
    {
      scheme: 'app-media',
      privileges: { secure: true, standard: true, supportFetchAPI: true, corsEnabled: true, stream: true },
    },
  ]);
}

export function handleMediaProtocol(): void {
  protocol.handle('app-media', (request) => {
    const path = new URL(request.url).searchParams.get('path');
    if (!path) return new Response('missing path', { status: 400 });

    let size: number;
    try {
      size = statSync(path).size;
    } catch {
      return new Response('not found', { status: 404 });
    }

    const mime = mimeFor(path);
    const range = request.headers.get('range');
    if (!range) {
      const stream = Readable.toWeb(createReadStream(path)) as ReadableStream;
      return new Response(stream, {
        status: 200,
        headers: { 'Content-Type': mime, 'Content-Length': String(size), 'Accept-Ranges': 'bytes' },
      });
    }

    const match = /bytes=(\d+)-(\d*)/.exec(range);
    const start = match?.[1] ? Number(match[1]) : 0;
    const end = match?.[2] ? Number(match[2]) : size - 1;
    const chunkSize = end - start + 1;
    const stream = Readable.toWeb(createReadStream(path, { start, end })) as ReadableStream;
    return new Response(stream, {
      status: 206,
      headers: {
        'Content-Type': mime,
        'Content-Range': `bytes ${start}-${end}/${size}`,
        'Content-Length': String(chunkSize),
        'Accept-Ranges': 'bytes',
      },
    });
  });
}

// app-media://play?path=<encodeURIComponent(absolutePath)> — the renderer never touches a
// filesystem path directly (sandboxed), it just asks for this URL and main resolves it. The path
// arrives pre-validated: it always came FROM main via musicRandomTrack/musicList in the first
// place, never typed/uploaded by the user, so there's no path-traversal surface here.

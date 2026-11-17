#!/usr/bin/env node
// Assembles the installer's read-only install root (build/genesis/): self-contained dps-nn.exe + the templates, schema and sample files the CLI reads at runtime; music only for the full edition.
//
// Usage (from gui/):  node scripts/stage-genesis.mjs [--rid win-x64] [--no-music]
//
// Layout contract (ADR-014): everything here is READ-ONLY at runtime and lands in
// <install>/resources/genesis/ via electron-builder's extraResources. The CLI finds it through
// AppContext.BaseDirectory (dps-nn.exe sits next to templates/); everything the CLI WRITES goes
// to its cwd, the workspace (Documents\DPS-NN), never here.
//
// Edition: the music is copied iff design/music/ has git-tracked media, so the same script builds
// the full installer in the main repo and the lite one in the music-free mirror.
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : fallback;
};
const rid = opt('--rid', 'win-x64');
const out = join(repo, 'build', 'genesis');

// Only git-TRACKED media: design/music/ locally also holds untracked files (.gitignore), and a
// local `npm run dist` must ship exactly what the release workflow ships from a clean checkout.
const trackedMusic = () => {
  try {
    return execFileSync('git', ['ls-files', '-z', 'design/music'], { cwd: repo, encoding: 'utf8' })
      .split('\0')
      .filter((f) => /\.(mp3|mp4|webm|mov)$/i.test(f) && existsSync(join(repo, f)));
  } catch {
    return []; // not a git checkout: treat as lite
  }
};
const music = args.includes('--no-music') ? [] : trackedMusic();
const withMusic = music.length > 0;

console.log(`stage-genesis: ${rid}, ${withMusic ? `full edition (${music.length} tracked media files)` : 'lite edition (no music)'} -> ${out}`);
rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

// 1. The CLI: self-contained single file, so the installer needs no `dotnet run` and no source.
//    The .NET 10 SDK is still a prerequisite - the pipeline drives `dotnet new/build/ef` for the
//    generated app (that is what `dps-nn doctor` checks).
execFileSync(
  'dotnet',
  [
    'publish', join(repo, 'src', 'Genesis.Cli'),
    '-c', 'Release', '-r', rid, '--self-contained',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PackAsTool=false', '-p:DebugType=none',
    '-o', out,
  ],
  { stdio: 'inherit' },
);
const exe = rid.startsWith('win') ? '.exe' : '';
renameSync(join(out, `GenesisV6.Cli${exe}`), join(out, `dps-nn${exe}`));

// 2. What the CLI reads at runtime relative to its GenesisRoot (Program.cs + Stages/*: every
//    Path.Combine(genesisRoot|ctx.GenesisRoot, ...)). Keep in sync with those call sites.
const copy = (rel) => cpSync(join(repo, rel), join(out, rel), { recursive: true });
for (const rel of [
  'templates',
  'feature-templates',
  'spec.schema.json',
  'images',
  'utils/claude-skills/dps-nn-spec-authoring', // create: the spec-authoring guide in the prompt
  'fixtures/project-spec.facenote.json', //      create: the worked example; GUI: the sample spec
  'seeds/facenote-rich-seed.json', //            GUI: the sample seed (Start works without a key)
  'design/footer.md', //                         GUI footer
  'LICENSE',
  'VERSION',
])
  copy(rel);
for (const f of music) copy(f);

// 3. Edition marker, read by .github/workflows/release.yml to name the artifact (Lite or not).
writeFileSync(join(out, 'install-root.json'), JSON.stringify({ edition: withMusic ? 'full' : 'lite' }, null, 2));

const size = (p) => (statSync(p).isDirectory() ? readdirSync(p).reduce((s, f) => s + size(join(p, f)), 0) : statSync(p).size);
console.log(`stage-genesis: done, ${(size(out) / 1e6).toFixed(0)} MB`);

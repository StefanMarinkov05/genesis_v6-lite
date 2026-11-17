// EN string table — the base/reference language and the source of the `Strings` shape.
// The other five languages (BG/DE/RU/Romani/SGA) were filled in phase 3b; each is typed
// `Strings` so it must match this shape exactly (see the Widen note at the bottom). Keys
// mirror the component vocabulary in design-specs.md §5.
//
// SCOPE BOUNDARY (do not conflate): this table is the GUI's OWN chrome language only. It is
// renderer-local and has ZERO effect on the language of a GENERATED app — those stay
// Bulgarian-only (CLAUDE.md hard requirement) unless the *spec's* meta.language says "en"
// (a completely separate axis, changelog 054). The main process is language-agnostic.
export const en = {
  title: 'Control Center',
  hq: 'DPS-NN HQ',
  win: { minimize: 'Minimize', maximize: 'Maximize', close: 'Close' },
  toolbar: { debug: 'Nerd mode', settings: 'Settings', reopen: 'Last result', recent: 'Recent projects' },
  spec: {
    heading: 'Specification',
    upload: 'Upload…',
    pick: 'Select…',
    none: 'No spec selected',
    describe: 'Or describe the project in your own words — Claude writes the spec, then generates and repairs it…',
    create: '✦ Generate from description',
  },
  seed: {
    heading: 'Seed',
    upload: 'Upload…',
    pick: 'Select…',
    none: 'No seed selected (optional)',
    claude: '✦ Claude',
    claudeHint: 'Claude seed generation — coming in phase 2',
  },
  run: {
    caption: 'Start / Pause / Resume / Stop — R2: locked across all themes',
    start: '▶ Start',
    pause: '❚❚ Pause',
    resume: '▶▶ Resume',
    stop: '■ Stop',
    pauseHint: 'Suspend after the current stage finishes',
    resumeHint: 'Continue from where it was paused',
  },
  progress: { idle: 'idle', of: (i: number, t: number) => `${i} / ${t}` },
  dup: {
    title: 'Duplicate build',
    lead: 'This specification + seed combination has already been generated.',
    project: 'Project',
    lastBuild: 'Last build',
    output: 'Output',
    open: 'Open existing',
    regen: 'Generate anyway',
    cancel: 'Cancel',
  },
  report: {
    title: 'Done — the application was generated',
    url: 'URL',
    credentials: 'Credentials',
    email: 'Email',
    password: 'Password',
    db: 'Database',
    start: 'Start',
    runApp: 'Run app',
    killApp: 'Kill app',
    openBrowser: 'Open in browser',
    openFolder: 'Open folder',
    close: 'Close',
    copy: 'copy',
    copied: 'copied',
  },
  error: {
    title: (stage: string) => `Build failed at stage "${stage}"`,
    stage: 'Stage',
    cause: 'Cause',
    retry: 'Retry from this stage',
    fullLog: 'Full log',
    stop: 'Stop',
  },
  settings: {
    title: 'Settings',
    apiKey: 'Anthropic API key',
    apiKeySet: '✓ Key is set (write-only)',
    apiKeyUnset: 'No key set — LLM stages will wait for manual input',
    apiKeyPlaceholder: 'sk-ant-…',
    genesisCmd: 'Genesis command',
    genesisCmdHint: 'For advanced/batch builds and debugging. Defaults to the bundled command.',
    outDir: 'Output folder',
    save: 'Save',
    cancel: 'Cancel',
  },
  recent: {
    title: 'Recent projects',
    empty: 'No projects generated yet.',
    lastBuilt: (ts: string) => `Last built ${ts}`,
    open: 'Open folder',
  },
  music: {
    button: 'Play Veselin Marinov',
    youtube: 'Open on YouTube',
    random: 'Play a random local track',
    pick: 'Choose a track…',
    concertsVideo: 'Play a concert (video)',
    stop: 'Stop',
    songsTab: 'Songs',
    concertsAudioTab: 'Concerts (audio)',
    concertsVideoTab: 'Concerts (video)',
    noTracks: 'No tracks found in this folder.',
  },
  wallpaper: {
    title: 'Wallpaper',
    novaAstroHud: 'Astral Gateway',
    novaCodeCorridor: 'Code Corridor',
    novaGerman: 'Das Deutsche Herz',
    imperatorPoster1: 'Veselin Marinov I',
    imperatorPoster2: 'Veselin Marinov II',
    imperatorPoster3: 'Veselin Marinov III',
    veilOccult: 'Veil of Isis',
    veilIslamic: 'Islamic Sufi Path',
    forge: 'Soul Forge',
    enchant: 'Astral Phoenix',
  },
  toast: {
    started: 'Build started',
    stageDone: (i: number, t: number) => `Stage ${i}/${t} complete`,
    done: 'Done',
    duplicate: 'Duplicate build found',
    stopped: 'Build stopped',
    paused: 'Build paused',
    resumed: 'Build resumed',
    failed: 'Build failed',
    awaitingLlm: 'Waiting for an LLM response',
    noKey: 'No API key set',
    settingsSaved: 'Settings saved',
    selectSpecFirst: 'Select a spec first',
    describeFirst: 'Describe the project first',
    agentStarted: 'Agent started',
    couldNotPlayTrack: 'Could not play the track',
    noLocalTracks: 'No local tracks found',
    noLocalTracksHint: 'Drop MP3/MP4 files into music/',
    specUploaded: 'Spec uploaded',
    seedUploaded: 'Seed uploaded',
    uploadFailed: 'Upload failed',
    couldNotStartApp: 'Could not start the app',
  },
  prank: {
    title: 'Performance notice',
    body:
      "The program is deliberately slowed down 10x. If you set the Veselin Marinov wallpaper it's " +
      "only 5x. If you play a short song, it runs at normal speed while it's playing. If you play " +
      'a concert and watch the video, extra threads get activated.',
    close: 'Close',
  },
} as const;

// The EN table is authored `as const`, so `typeof en` has narrow *literal* string types
// ('Minimize', not string). A translation table must satisfy the SAME SHAPE but obviously
// with different string values — so we widen the literals to `string` (preserving nested
// objects and function signatures like `progress.of`). Every other language table is typed
// `Strings`, which makes a missing/renamed/mistyped key a COMPILE error (tsc), not a runtime
// surprise — the "later i18n pass is a table fill, not a re-wire" guarantee, enforced.
type Widen<T> = T extends (...args: infer A) => infer R
  ? (...args: A) => R
  : T extends string
    ? string
    : { [K in keyof T]: Widen<T[K]> };

export type Strings = Widen<typeof en>;

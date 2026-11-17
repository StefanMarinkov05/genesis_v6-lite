// Renderer mirror of the run-control state machine in docs/explanation/gui-architecture.md.
// It decides only which run-control buttons are legal in each state (a button is enabled
// only where its transition is valid, per design-specs.md §5).
//
// Phase 2 (changelog 102): Pause/Resume wired to the UI, now that the manual npm-run-dev gate
// is cleared. 'paused' is a DISTINCT state from 'stopped' — Stop means "abandon this run" (Start
// begins a fresh one), Pause means "suspend, expecting Resume" (only Resume legal, not Start) —
// collapsing them onto one state would have made Resume appear after a Stop too, which is wrong.

export type RunState = 'idle' | 'running' | 'awaiting-llm' | 'paused' | 'done' | 'error' | 'stopped';

export interface ButtonEnablement {
  start: boolean;
  pause: boolean;
  resume: boolean;
  stop: boolean;
}

export function enablement(state: RunState): ButtonEnablement {
  const canStart = state === 'idle' || state === 'done' || state === 'error' || state === 'stopped';
  const canStop = state === 'running' || state === 'awaiting-llm';
  return {
    start: canStart,
    stop: canStop,
    pause: state === 'running',
    resume: state === 'paused',
  };
}

/** The button whose glow "pulse" should animate (the live action), or null. */
export function activeButton(state: RunState): keyof ButtonEnablement | null {
  return state === 'running' || state === 'awaiting-llm' ? 'start' : null;
}

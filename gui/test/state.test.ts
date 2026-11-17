import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { activeButton, enablement, type RunState } from '../src/renderer/state.ts';

// Guards the run-control state machine (docs/explanation/gui-architecture.md), especially the
// 'paused' vs 'stopped' distinction added when Pause/Resume was wired to the UI (changelog 102):
// Stop means "abandon, Start begins fresh"; Pause means "suspend, only Resume is legal next" —
// collapsing them onto one state would make Resume appear after a plain Stop too, which is wrong.

const ALL_STATES: RunState[] = ['idle', 'running', 'awaiting-llm', 'paused', 'done', 'error', 'stopped'];

test('resume is legal ONLY from paused', () => {
  for (const s of ALL_STATES) {
    assert.equal(enablement(s).resume, s === 'paused', `resume enablement wrong for state "${s}"`);
  }
});

test('pause is legal ONLY from running', () => {
  for (const s of ALL_STATES) {
    assert.equal(enablement(s).pause, s === 'running', `pause enablement wrong for state "${s}"`);
  }
});

test('start is legal from idle/done/error/stopped, never from paused', () => {
  assert.equal(enablement('paused').start, false);
  assert.equal(enablement('idle').start, true);
  assert.equal(enablement('done').start, true);
  assert.equal(enablement('error').start, true);
  assert.equal(enablement('stopped').start, true);
  assert.equal(enablement('running').start, false);
  assert.equal(enablement('awaiting-llm').start, false);
});

test('stop is legal only from running/awaiting-llm', () => {
  assert.equal(enablement('running').stop, true);
  assert.equal(enablement('awaiting-llm').stop, true);
  assert.equal(enablement('paused').stop, false);
  assert.equal(enablement('idle').stop, false);
});

test('the active (pulsing) button is Start during running/awaiting-llm, none otherwise', () => {
  assert.equal(activeButton('running'), 'start');
  assert.equal(activeButton('awaiting-llm'), 'start');
  assert.equal(activeButton('paused'), null);
  assert.equal(activeButton('idle'), null);
});

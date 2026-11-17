/// <reference types="vite/client" />
import type { GuiApi } from '@shared/ipc-types';

// The preload exposes the typed GuiApi as window.gui (contextBridge). This declaration
// lets the renderer call it with full type-checking against the same contract.
declare global {
  interface Window {
    gui: GuiApi;
  }
}

export {};

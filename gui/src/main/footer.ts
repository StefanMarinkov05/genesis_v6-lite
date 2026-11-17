import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { installRoot } from './paths';

// The footer is read at RUNTIME from design/footer.md (never bundled) so it can change
// without a rebuild (spec B.1 + gui-architecture.md). Empty or missing = render nothing,
// never error.
export function getFooter(): string {
  const path = join(installRoot(), 'design', 'footer.md');
  if (!existsSync(path)) return '';
  try {
    return readFileSync(path, 'utf8').trim();
  } catch {
    return '';
  }
}

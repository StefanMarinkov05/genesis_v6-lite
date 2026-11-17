<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-006: Scaffold deviations via regex on known scaffolder shapes

**Status**: accepted

**Context**: spec-declared deviations (removedActions, redirects, controller sorting) must transform aspnet-codegenerator output.

**Decision**: targeted regex transforms anchored on the stable scaffolder shapes of the pinned SDK, each FAILING LOUD when its anchor is missing. Not Roslyn: a syntax-tree rewriter is 10x the code for output that is stable under an SDK pin, and the fail-loud anchors surface SDK drift immediately.

**Consequences**: SDK bump = recheck DeviationTests against one real scaffold; transforms stay ~30 lines each.

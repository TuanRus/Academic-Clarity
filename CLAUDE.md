# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
npm run dev        # Start dev server at http://localhost:5173
npm run build      # Type-check (tsc -b) then bundle for production
npm run lint       # Run ESLint
npx tsc -b         # Type-check only, no emit
```

No test runner is configured yet.

## Tech Stack

- **React 19** + **TypeScript 6** + **Vite 8** + **Tailwind CSS 3**
- **React Compiler** enabled via `@rolldown/plugin-babel` + `babel-plugin-react-compiler` (see `vite.config.ts`)
- **react-router-dom v6** for routing

## TypeScript Strictness

`tsconfig.app.json` enables two non-default flags that affect all new code:

- **`verbatimModuleSyntax: true`** — type-only imports must use `import type { X }` or `import { type X }`. Mixing value and type imports in one statement: `import { SomeClass, type SomeInterface }`.
- **`erasableSyntaxOnly: true`** — TypeScript `enum` is forbidden. Use `as const` objects + a derived union type instead:

```typescript
export const Role = { ADMIN: 'ADMIN', ... } as const;
export type Role = typeof Role[keyof typeof Role];
```

- **React 19 event types**: `FormEvent` and other synthetic event types are deprecated. Use structural typing `(e: { preventDefault(): void })` or native DOM event types.

## Permission Model (Two-Axis)

The core domain concept spans three files:

1. **`src/types/auth.ts`** — defines `Role` (academic identity: STUDENT/LECTURER/RESEARCHER/ADMIN) and `AccessTier` (BASIC/PREMIUM). These are independent axes.
2. **`src/types/permissions.ts`** — defines `FeaturePermission` codes (e.g. `GRAPH_ADVANCED`, `EXPORT_CSV`).
3. **`src/lib/permissions.ts`** — the single source of truth for feature access:
   - `hasFeature(user, feature)` — mirrors the backend `hasFeature()`: ADMIN bypasses tier checks (BR-26), everyone else is gated by `AccessTier`.
   - `isAdmin(user)` — role-only check for admin area access (FR-27/28), independent of tier.

**Rule**: Never check `user.role` or `user.accessTier` directly to decide feature visibility. Always go through `hasFeature()` / `useFeature()`.

## Access Control in the UI

Two mechanisms for gating content:

- **`src/routes/RequireAuth.tsx`** — route-level guard; redirects unauthenticated users to `/login`, preserving `location.state.from` for post-login redirect.
- **`src/routes/RequireFeature.tsx`** — UI-block-level wrapper (not a route guard); renders `<UpgradeOverlay>` when the user lacks the required `FeaturePermission`. Use this to gate individual sections within a page (e.g. the advanced graph panel stays hidden while the basic panel renders).

## Provider Tree

```
<BrowserRouter>
  <AuthProvider>        // src/context/AuthContext.tsx
    <BookmarkProvider>  // src/context/BookmarkContext.tsx
      <AppRoutes />
    </BookmarkProvider>
  </AuthProvider>
</BrowserRouter>
```

Consume via hooks only: `useAuth()`, `useBookmark()`, `useFeature(permission)`. Calling these outside their provider throws an error immediately.

## Mock Data & Demo Helpers

`src/mock/users.ts` seeds four users covering every role/tier combination. `AuthContext` exposes demo-only methods (`loginAsMock`, `upgradeToPremium`, `downgradeToBasic`) used by the dev switcher in `Header.tsx` — these are intentionally not real API calls.

## Route Structure

All routes except `/login` and `/register` require authentication. Authenticated routes are wrapped in `<MainLayout>` (Header + `<Outlet>`). See `src/routes/AppRoutes.tsx` for the full map.

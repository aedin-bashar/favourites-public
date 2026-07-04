// `core/` — application-wide singletons that ship with the running app:
// authentication, route guards, HTTP interceptors, and infrastructure
// services. Code here is imported by `app.config.ts` and the root shell.
//
// Source: docs/ARCHITECTURE.md §8.

export * from './api';
export * from './auth';

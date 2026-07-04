/**
 * Shape of the environment configuration. Lives in its own file so that
 * `environment.ts` and `environment.production.ts` can both import the
 * type without referring to each other — Angular's `fileReplacements`
 * substitutes `environment.ts` with `environment.production.ts` at
 * production-build time, which would otherwise create a circular import.
 */
export interface Environment {
  readonly production: boolean;
}

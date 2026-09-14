const storageKey = 'dsa-practice.submitter-id';

/**
 * Identifies the browser making submissions, until real accounts arrive in items 19-20.
 *
 * Deliberately not a login: it is a random id in localStorage, worth nothing and proving nothing.
 * The Api takes a userId on the request today, which item 19 replaces with the identity on a
 * validated token -- at which point this file goes away.
 */
export function getSubmitterId(): string {
  try {
    const existing = localStorage.getItem(storageKey);
    if (existing) {
      return existing;
    }

    const created = `anon-${crypto.randomUUID()}`;
    localStorage.setItem(storageKey, created);
    return created;
  } catch {
    // Private mode, or storage disabled: a per-session id still lets someone submit.
    return `anon-${crypto.randomUUID()}`;
  }
}

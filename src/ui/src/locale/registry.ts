import { switzerland } from './ch';
import { german } from './de';
import type { Locale } from './types';

const germanSwiss: Locale = { name: 'de-CH', language: german, formats: switzerland };

/** Every locale the UI knows, by the name the host passes; a new one is a new entry here and changes no screen. */
const locales: ReadonlyMap<string, Locale> = new Map([[germanSwiss.name, germanSwiss]]);

/** The locale the host named, or German with Swiss formats when it named none the UI knows. */
export function localeNamed(name: string | null): Locale {
  return (name !== null ? locales.get(name) : undefined) ?? germanSwiss;
}

// Compile-time probes, checked by the UI type check and never bundled. Each sample below is a copy of the German texts
// with one text removed; the type check must reject it. Should a sample ever compile, its @ts-expect-error is unused,
// which is itself an error, and verify fails: a language can no longer lack a text unnoticed.
import { german } from '../src/locale/de';
import type { Language } from '../src/locale/types';

const { total: _weekTotal, ...weekWithoutTotal } = german.week;
// @ts-expect-error A language lacking one screen text is no language.
export const lacksScreenText: Language = { ...german, week: weekWithoutTotal };

const { notMonday: _notMonday, ...refusalsWithoutOne } = german.refusals;
// @ts-expect-error A language lacking the text of one refusal code is no language.
export const lacksRefusalText: Language = { ...german, refusals: refusalsWithoutOne };

const [january, february, march, april, may, june, july, august, september, october, november] = german.months;
export const lacksMonthName: Language = {
  ...german,
  // @ts-expect-error A language lacking one month name is no language.
  months: [january, february, march, april, may, june, july, august, september, october, november],
};

const { zh: _zurich, ...cantonsWithoutOne } = german.cantons;
// @ts-expect-error A language lacking the name of one canton is no language.
export const lacksCantonName: Language = { ...german, cantons: cantonsWithoutOne };

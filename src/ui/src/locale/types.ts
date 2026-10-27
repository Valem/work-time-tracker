import type { CalendarDay, IsoWeek, PauseRole, RefusalCode } from '../bridge/contract';

type Weekdays = readonly [string, string, string, string, string, string, string];

/**
 * One language: every text the screens show, and one for each refusal code. A language that lacks any of them does not
 * type-check, so adding a language means adding one complete definition.
 */
export interface Language {
  /** The language's tag for the page, such as "de". */
  readonly tag: string;
  readonly productName: string;
  /** Monday to Sunday, short. */
  readonly weekdays: Weekdays;
  /** A weekday and a formatted date, as one label. */
  readonly dayLabel: (weekday: string, date: string) => string;
  /** A day and a time, as one label. */
  readonly moment: (day: string, time: string) => string;
  readonly span: (start: string, end: string) => string;
  readonly empty: string;
  readonly loading: string;
  readonly nav: {
    readonly label: string;
    readonly week: string;
    readonly settings: string;
  };
  readonly status: {
    readonly recording: (last: string) => string;
    readonly stopped: (since: string) => string;
    readonly never: string;
  };
  readonly week: {
    readonly heading: (week: string, from: string, to: string) => string;
    readonly previous: string;
    readonly previousHint: string;
    readonly next: string;
    readonly nextHint: string;
    readonly pause: (duration: string) => string;
    readonly total: (duration: string) => string;
    readonly rebuilt: string;
    readonly rebuiltHint: string;
    readonly openDay: (day: string) => string;
  };
  readonly day: {
    readonly back: string;
    readonly start: string;
    readonly end: string;
    readonly break: string;
    readonly total: string;
    readonly noUse: string;
    readonly timeline: string;
    readonly work: string;
    readonly eveningUse: string;
    readonly ignoredWake: string;
    readonly roles: Readonly<Record<PauseRole, string>>;
    readonly explained: string;
    readonly nightPause: (span: string, duration: string) => string;
    readonly lunchWhole: (span: string, duration: string) => string;
    readonly lunchRaised: (span: string, duration: string, deducted: string) => string;
    readonly lunchMinimumOnly: (deducted: string) => string;
    readonly noBreak: string;
    readonly pauseAsWork: (span: string, duration: string) => string;
    readonly wake: (time: string) => string;
    readonly eveningPause: (span: string, duration: string, after: string) => string;
  };
  readonly settings: {
    readonly heading: string;
    readonly prefilled: string;
    readonly validFrom: (date: string) => string;
    readonly window: string;
    readonly windowFrom: string;
    readonly windowTo: string;
    readonly tiers: string;
    readonly tiersHint: string;
    readonly tierOver: string;
    readonly tierMinimum: string;
    readonly addTier: string;
    readonly removeTier: string;
    readonly noTiers: string;
    readonly save: string;
    readonly saving: string;
    readonly saved: (date: string) => string;
    readonly unreadable: string;
  };
  readonly refusals: Readonly<Record<RefusalCode, string>>;
}

/**
 * One region's formats: how dates, weeks, times and durations read, and how a typed time or duration reads back into
 * minutes (null when it cannot). Values are only formatted here; every number comes from the host.
 */
export interface Formats {
  readonly date: (day: CalendarDay) => string;
  readonly week: (week: IsoWeek) => string;
  /** Minutes after midnight; 1440 is the end of the day. */
  readonly time: (minutes: number) => string;
  readonly duration: (minutes: number) => string;
  readonly timePlaceholder: string;
  readonly durationPlaceholder: string;
  readonly readTime: (text: string) => number | null;
  readonly readDuration: (text: string) => number | null;
}

/** A language joined with a region's formats, under the name the host passes in the start page's address. */
export interface Locale {
  readonly name: string;
  readonly language: Language;
  readonly formats: Formats;
}

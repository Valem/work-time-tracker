import type {
  CalendarDay,
  Canton,
  DayType,
  EditOutcome,
  IsoWeek,
  LoadReason,
  PauseRole,
  RefusalCode,
} from '../bridge/contract';

type Weekdays = readonly [string, string, string, string, string, string, string];

/** January to December. */
export type Months = readonly [string, string, string, string, string, string, string, string, string, string, string, string];

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
  readonly months: Months;
  readonly monthsShort: Months;
  /** Each canton by the code the host sends. */
  readonly cantons: Readonly<Record<Canton, string>>;
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
    readonly planner: string;
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
  /** What counts on a day of the week, beside its times; a public holiday's with the names of those that count. */
  readonly marks: {
    readonly holiday: string;
    readonly halfHoliday: string;
    readonly publicHoliday: (names: readonly string[]) => string;
    readonly halfPublicHoliday: (names: readonly string[]) => string;
    readonly freeDay: string;
  };
  readonly planner: Planner;
  readonly myYear: MyYear;
  /** Where the canton's public holidays of the shown year stand. */
  readonly loads: {
    readonly noCanton: string;
    readonly loading: string;
    readonly loaded: (date: string) => string;
    readonly notLoaded: string;
    readonly notLoadedBecause: (reason: string) => string;
    readonly reasons: Readonly<Record<LoadReason, string>>;
    readonly outOfRange: string;
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
    /** One kind in the timeline's legend, with the span or time of each place it shows that day. */
    readonly legendEntry: (kind: string, times: readonly string[]) => string;
    readonly explained: string;
    readonly nightPause: (span: string, duration: string) => string;
    readonly lunchWhole: (span: string, duration: string) => string;
    readonly lunchRaised: (span: string, duration: string, deducted: string) => string;
    readonly lunchMinimumOnly: (deducted: string) => string;
    readonly noBreak: string;
    readonly pauseAsWork: (span: string, duration: string) => string;
    /** The times Windows woke alone on this day; it produced no use. */
    readonly windowsWakes: (times: readonly string[]) => string;
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

/** The year screen: the counts bar, the year grid, its legend and the message after a change. */
export interface Planner {
  readonly grid: (year: string) => string;
  readonly previousYear: (year: string) => string;
  readonly nextYear: (year: string) => string;
  readonly yearly: string;
  readonly taken: string;
  readonly planned: string;
  readonly left: string;
  readonly meter: (taken: string, planned: string, yearly: string) => string;
  /** While dragging: the dates covered, the change in days and what is left after it. */
  readonly live: (span: string, change: string, left: string) => string;
  readonly layout: string;
  readonly columns: string;
  readonly rows: string;
  readonly myYear: string;
  readonly kinds: Readonly<Record<DayType, string>>;
  readonly halfPublicHoliday: string;
  readonly holiday: string;
  readonly halfHoliday: string;
  readonly countsNothing: string;
  readonly offered: (name: string) => string;
  readonly granted: (name: string) => string;
  readonly bridge: string;
  readonly today: string;
  /** What a click on the day would change. */
  readonly onClick: Readonly<Record<EditOutcome, string>>;
  readonly halfOnlyByDrag: string;
  readonly dayTitle: (day: string, parts: readonly string[]) => string;
  readonly legend: {
    readonly holiday: string;
    readonly halfHoliday: string;
    readonly publicHoliday: string;
    readonly offered: string;
    readonly freeDay: string;
    readonly weekend: string;
    readonly bridge: string;
  };
  /** A change of days, its size already formatted, the change in half days given for the plural: 1 Tag, 6 Tage. */
  readonly days: (amount: string, halfDays: number) => string;
  readonly edits: {
    readonly booked: (span: string, days: string) => string;
    readonly removed: (span: string, days: string) => string;
    readonly bookedWhole: (date: string) => string;
    readonly bookedHalf: (date: string) => string;
    readonly ticked: (name: string, date: string) => string;
    readonly unticked: (name: string, date: string) => string;
  };
  readonly undo: string;
  readonly undone: string;
}

/** «Mein Jahr»: the year's settings, the public holidays to tick, their load state and source. */
export interface MyYear {
  readonly heading: (year: string) => string;
  readonly scope: (year: string) => string;
  readonly close: string;
  readonly pensum: string;
  readonly pensumUnit: string;
  readonly weekdays: string;
  readonly working: string;
  readonly free: string;
  readonly yearlyDays: string;
  readonly prefilled: (days: string) => string;
  readonly canton: string;
  readonly noCanton: string;
  readonly cantonHint: string;
  readonly offered: string;
  readonly noneOffered: string;
  readonly offeredEntry: (name: string, date: string, half: boolean) => string;
  readonly reload: string;
  readonly reloading: string;
  readonly attribution: (source: string, licence: string) => string;
  readonly save: string;
  readonly saving: string;
  readonly saved: string;
  readonly unreadable: string;
  /** A refusal of «Mein Jahr» for a value outside what it takes, in place of the general text for that code. */
  readonly outOfRange: string;
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
  readonly year: (year: number) => string;
  /** The day of the month alone, as a cell shows it. */
  readonly dayOfMonth: (day: CalendarDay) => string;
  /** A date without its year, as the start of a span within one year. */
  readonly shortDate: (day: CalendarDay) => string;
  /** An amount of days given in half days: 33 reads 16.5. */
  readonly halfDays: (halfDays: number) => string;
  /** A change in half days, with its sign: +6, −2. */
  readonly signedHalfDays: (halfDays: number) => string;
  /** The size of a change in half days, without its sign. */
  readonly halfDaysAmount: (halfDays: number) => string;
  /** A typed number of days in halves, such as 16.5, as half days; null when it does not read. */
  readonly readHalfDays: (text: string) => number | null;
  /** A typed whole number; null when it does not read. */
  readonly readWhole: (text: string) => number | null;
}

/** A language joined with a region's formats, under the name the host passes in the start page's address. */
export interface Locale {
  readonly name: string;
  readonly language: Language;
  readonly formats: Formats;
}

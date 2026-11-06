import { createContext } from 'preact';
import { useContext } from 'preact/hooks';
import type { CalendarDay, ClockReading, DaySpan } from '../bridge/contract';
import type { Formats, Language, Locale } from './types';

/** The locale's texts, its formats, and the few labels every screen builds from both. */
export interface Words {
  readonly language: Language;
  readonly formats: Formats;
  readonly day: (day: CalendarDay) => string;
  readonly reading: (reading: ClockReading) => string;
  readonly span: (span: DaySpan) => string;
  readonly weekday: (day: CalendarDay) => string;
  /** The dates from one day to another within a year: 22.03. – 31.03.2027, or one date where they are the same. */
  readonly dates: (from: CalendarDay, to: CalendarDay) => string;
}

export function wordsOf(locale: Locale): Words {
  const { language, formats } = locale;
  const weekday = (calendarDay: CalendarDay): string => language.weekdays[calendarDay.weekday - 1] ?? language.empty;
  const day = (calendarDay: CalendarDay): string => language.dayLabel(weekday(calendarDay), formats.date(calendarDay));
  return {
    language,
    formats,
    day,
    reading: (reading) => language.moment(day(reading.day), formats.time(reading.minutes)),
    span: (span) => language.span(formats.time(span.start), formats.time(span.end)),
    weekday,
    dates: (from, to) =>
      from.date === to.date ? formats.date(from) : language.span(formats.shortDate(from), formats.date(to)),
  };
}

export const WordsContext = createContext<Words | null>(null);

export function useWords(): Words {
  const words = useContext(WordsContext);
  if (words === null) {
    throw new Error('No locale above this component.');
  }

  return words;
}

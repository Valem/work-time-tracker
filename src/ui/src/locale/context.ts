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
}

export function wordsOf(locale: Locale): Words {
  const { language, formats } = locale;
  const day = (calendarDay: CalendarDay): string =>
    language.dayLabel(language.weekdays[calendarDay.weekday - 1] ?? language.empty, formats.date(calendarDay));
  return {
    language,
    formats,
    day,
    reading: (reading) => language.moment(day(reading.day), formats.time(reading.minutes)),
    span: (span) => language.span(formats.time(span.start), formats.time(span.end)),
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

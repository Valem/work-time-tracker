import type { Formats } from './types';

const twoDigits = (value: number): string => String(value).padStart(2, '0');

/** Hours and minutes, as 12:30 or 12.30; null unless the minutes are 00 to 59. */
function readHoursAndMinutes(text: string): number | null {
  const match = /^\s*(\d{1,2})[:.](\d{2})\s*$/.exec(text);
  if (!match) {
    return null;
  }

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  return minutes < 60 ? hours * 60 + minutes : null;
}

const minus = '−';

/** Half days as days with a decimal point and at most one place: 33 reads 16.5, −3 reads −1.5. */
function halfDays(value: number): string {
  const size = Math.abs(value);
  return `${value < 0 ? minus : ''}${Math.floor(size / 2)}${size % 2 === 1 ? '.5' : ''}`;
}

/** Days as 16.5 or 16,5 — whole or a half — into half days; a leading minus is kept for the host to judge. */
function readHalfDays(text: string): number | null {
  const match = /^\s*([-−]?)(\d{1,4})(?:[.,](\d))?\s*$/.exec(text);
  if (!match) {
    return null;
  }

  const fraction = match[3] ?? '0';
  if (fraction !== '0' && fraction !== '5') {
    return null;
  }

  const size = Number(match[2]) * 2 + (fraction === '5' ? 1 : 0);
  return match[1] === '' ? size : -size;
}

function readWhole(text: string): number | null {
  const match = /^\s*([-−]?)(\d{1,4})\s*$/.exec(text);
  return match ? (match[1] === '' ? 1 : -1) * Number(match[2]) : null;
}

function dateParts(day: { readonly date: string }): readonly [string, string, string] {
  const [year = '', month = '', date = ''] = day.date.split('-');
  return [year, month, date];
}

/** Swiss formats: 19.10.2026, 08:15 (24:00 at the end of the day), durations as 8:00 and 41:02, days as 16.5. */
export const switzerland: Formats = {
  date: (day) => {
    const [year, month, date] = dateParts(day);
    return `${date}.${month}.${year}`;
  },
  week: (week) => String(week.number),
  time: (minutes) => `${twoDigits(Math.floor(minutes / 60))}:${twoDigits(minutes % 60)}`,
  duration: (minutes) => `${Math.floor(minutes / 60)}:${twoDigits(minutes % 60)}`,
  timePlaceholder: 'hh:mm',
  durationPlaceholder: 'h:mm',
  readTime: readHoursAndMinutes,
  readDuration: readHoursAndMinutes,
  year: (year) => String(year),
  dayOfMonth: (day) => String(Number(dateParts(day)[2])),
  shortDate: (day) => {
    const [, month, date] = dateParts(day);
    return `${date}.${month}.`;
  },
  halfDays,
  signedHalfDays: (value) => (value > 0 ? `+${halfDays(value)}` : halfDays(value)),
  halfDaysAmount: (value) => halfDays(Math.abs(value)),
  readHalfDays,
  readWhole,
};

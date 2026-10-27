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

/** Swiss formats: 19.10.2026, 08:15 (24:00 at the end of the day), durations as 8:00 and 41:02. */
export const switzerland: Formats = {
  date: (day) => {
    const [year, month, date] = day.date.split('-');
    return `${date}.${month}.${year}`;
  },
  week: (week) => String(week.number),
  time: (minutes) => `${twoDigits(Math.floor(minutes / 60))}:${twoDigits(minutes % 60)}`,
  duration: (minutes) => `${Math.floor(minutes / 60)}:${twoDigits(minutes % 60)}`,
  timePlaceholder: 'hh:mm',
  durationPlaceholder: 'h:mm',
  readTime: readHoursAndMinutes,
  readDuration: readHoursAndMinutes,
};

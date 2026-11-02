import { useEffect, useRef, useState } from 'preact/hooks';
import type { MonthView, PlanDay, PreviewView, YearView } from '../bridge/contract';
import { useWords, type Words } from '../locale/context';

interface YearGridProps {
  readonly view: YearView;
  /** The latest preview of the drag under way, or null. */
  readonly preview: PreviewView | null;
  readonly onPreview: (anchor: string, to: string) => void;
  readonly onDrag: (anchor: string, to: string) => void;
  readonly onDragDropped: () => void;
  readonly onClick: (date: string) => void;
}

interface Drag {
  readonly anchor: string;
  to: string;
  moved: boolean;
}

type Gestures = Pick<YearGridProps, 'onPreview' | 'onDrag' | 'onDragDropped' | 'onClick'>;

/** Which arrow keys move along the days and which across the months, in each layout. */
const Arrows = {
  columns: { next: 'ArrowDown', previous: 'ArrowUp', nextMonth: 'ArrowRight', previousMonth: 'ArrowLeft' },
  rows: { next: 'ArrowRight', previous: 'ArrowLeft', nextMonth: 'ArrowDown', previousMonth: 'ArrowUp' },
} as const;

function dateAt(target: EventTarget | null): string | undefined {
  return target instanceof Element ? target.closest<HTMLElement>('[data-date]')?.dataset['date'] : undefined;
}

function firstFocus(view: YearView): string {
  const days = view.months.flatMap((month) => month.days);
  return (days.find((day) => day.today) ?? days[0])?.day.date ?? '';
}

/**
 * The day the key moves to, by the days' places in the grid as the host laid them out: along the month to the next or
 * previous day, across to the same place in the next or previous month, Home and End to the month's first and last day.
 */
function dayAfterKey(view: YearView, date: string, key: string): string | undefined {
  const arrows = Arrows[view.layout];
  const months = view.months;
  const month = months.findIndex((candidate) => candidate.days.some((day) => day.day.date === date));
  const days = months[month]?.days ?? [];
  const place = days.findIndex((day) => day.day.date === date);
  const all = months.flatMap((candidate) => candidate.days);
  const at = all.findIndex((day) => day.day.date === date);
  const inMonth = (other: MonthView | undefined): string | undefined =>
    other?.days[Math.min(place, other.days.length - 1)]?.day.date;
  switch (key) {
    case arrows.next:
      return all[at + 1]?.day.date;
    case arrows.previous:
      return all[at - 1]?.day.date;
    case arrows.nextMonth:
      return inMonth(months[month + 1]);
    case arrows.previousMonth:
      return inMonth(months[month - 1]);
    case 'Home':
      return days[0]?.day.date;
    case 'End':
      return days[days.length - 1]?.day.date;
    default:
      return undefined;
  }
}

/**
 * The year, each month in its own panel, as columns or as rows. Each day is a button: a press and release on one day is
 * a click; reaching another day while pressed starts a drag, previewed at each new day and sent on release; Escape drops
 * it. The keyboard reaches each day with Tab and the arrow keys, and Enter or Space does what a click does.
 */
export function YearGrid({ view, preview, onPreview, onDrag, onDragDropped, onClick }: YearGridProps) {
  const words = useWords();
  const grid = useRef<HTMLDivElement>(null);
  const drag = useRef<Drag | null>(null);
  const gestures = useRef<Gestures>({ onPreview, onDrag, onDragDropped, onClick });
  gestures.current = { onPreview, onDrag, onDragDropped, onClick };
  const [focusDate, setFocusDate] = useState(() => firstFocus(view));

  useEffect(() => setFocusDate(firstFocus(view)), [view.year]);

  useEffect(() => {
    const release = (): void => {
      const ended = drag.current;
      drag.current = null;
      if (ended === null) {
        return;
      }

      if (ended.moved) {
        gestures.current.onDrag(ended.anchor, ended.to);
      } else {
        gestures.current.onClick(ended.anchor);
      }
    };
    const drop = (): void => {
      if (drag.current !== null) {
        drag.current = null;
        gestures.current.onDragDropped();
      }
    };
    const escape = (event: KeyboardEvent): void => {
      if (event.key === 'Escape' && drag.current !== null) {
        event.preventDefault();
        drop();
      }
    };

    document.addEventListener('pointerup', release);
    document.addEventListener('pointercancel', drop);
    document.addEventListener('keydown', escape);
    window.addEventListener('blur', drop);
    return () => {
      document.removeEventListener('pointerup', release);
      document.removeEventListener('pointercancel', drop);
      document.removeEventListener('keydown', escape);
      window.removeEventListener('blur', drop);
    };
  }, []);

  const moveFocus = (date: string): void => {
    setFocusDate(date);
    grid.current?.querySelector<HTMLElement>(`[data-date="${date}"]`)?.focus();
  };

  const pointerDown = (event: PointerEvent): void => {
    const date = dateAt(event.target);
    if (event.button !== 0 || date === undefined) {
      return;
    }

    event.preventDefault();
    moveFocus(date);
    drag.current = { anchor: date, to: date, moved: false };
  };

  const pointerOver = (event: PointerEvent): void => {
    const current = drag.current;
    const date = dateAt(event.target);
    if (current === null || date === undefined || date === current.to) {
      return;
    }

    current.to = date;
    current.moved = true;
    gestures.current.onPreview(current.anchor, date);
  };

  const keyDown = (event: KeyboardEvent): void => {
    const date = dateAt(event.target);
    if (date === undefined) {
      return;
    }

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!event.repeat && drag.current === null) {
        gestures.current.onClick(date);
      }

      return;
    }

    const next = dayAfterKey(view, date, event.key);
    if (next !== undefined) {
      event.preventDefault();
      moveFocus(next);
    }
  };

  const previewed = new Map(preview?.days.map((day) => [day.date, preview.mode]) ?? []);
  const { language } = words;
  return (
    <div
      ref={grid}
      class="year-grid"
      data-layout={view.layout}
      aria-label={language.planner.grid(words.formats.year(view.year))}
      onPointerDown={pointerDown}
      onPointerOver={pointerOver}
      onKeyDown={keyDown}
    >
      {view.months.map((month) => (
        <div class="month" role="group" key={month.month} aria-label={language.months[month.month - 1]}>
          <div class="month-name" aria-hidden="true">
            {view.layout === 'columns' ? language.months[month.month - 1] : language.monthsShort[month.month - 1]}
          </div>
          {month.days.map((day) => (
            <DayCell
              key={day.day.date}
              day={day}
              words={words}
              focused={day.day.date === focusDate}
              previewed={previewed.get(day.day.date)}
            />
          ))}
        </div>
      ))}
    </div>
  );
}

/** The names on a date: of the public holidays that count, then of those offered, each once. */
function namesOn(day: PlanDay): string[] {
  const ordered = [...day.publicHolidays.filter((holiday) => holiday.counts), ...day.publicHolidays.filter((holiday) => !holiday.counts)];
  return [...new Set(ordered.map((holiday) => holiday.name))];
}

/** Everything the day's title says: what it is, what is booked and counts, the holidays on it, and what a click does. */
function titleOf(day: PlanDay, words: Words): string {
  const { planner, marks } = words.language;
  const byItself = [...new Set(day.publicHolidays.filter((holiday) => holiday.counts && !holiday.offered).map((holiday) => holiday.name))];
  const half = day.publicHolidayShare === 'half';
  const parts: string[] = [];
  if (day.kind === 'publicHoliday' && byItself.length > 0) {
    parts.push(half ? marks.halfPublicHoliday(byItself) : marks.publicHoliday(byItself));
  } else if (day.kind === 'publicHoliday') {
    parts.push(half ? planner.halfPublicHoliday : planner.kinds.publicHoliday);
  } else {
    parts.push(planner.kinds[day.kind]);
  }

  if (day.counted === 2) {
    parts.push(planner.holiday);
  } else if (day.counted === 1) {
    parts.push(planner.halfHoliday);
  } else if (day.booking !== null) {
    parts.push(planner.countsNothing);
  }

  for (const holiday of day.publicHolidays.filter((entry) => entry.offered)) {
    parts.push(holiday.ticked ? planner.granted(holiday.name) : planner.offered(holiday.name));
  }

  if (day.bridge) {
    parts.push(planner.bridge);
  }

  if (day.today) {
    parts.push(planner.today);
  }

  parts.push(planner.onClick[day.onClick]);
  return planner.dayTitle(words.day(day.day), parts);
}

interface DayCellProps {
  readonly day: PlanDay;
  readonly words: Words;
  readonly focused: boolean;
  readonly previewed: 'book' | 'erase' | undefined;
}

function DayCell({ day, words, focused, previewed }: DayCellProps) {
  const title = titleOf(day, words);
  const offered = day.publicHolidays.some((holiday) => holiday.offered && !holiday.counts);
  return (
    <button
      type="button"
      class="plan-day"
      tabIndex={focused ? 0 : -1}
      title={title}
      aria-label={title}
      data-date={day.day.date}
      data-weekday={day.day.weekday}
      data-kind={day.kind}
      data-public={day.publicHolidayShare}
      data-counted={day.counted}
      data-stale={(day.booking !== null && day.counted === 0) || undefined}
      data-offered={offered || undefined}
      data-bridge={day.bridge || undefined}
      data-past={day.past || undefined}
      data-today={day.today || undefined}
      data-click={day.onClick}
      data-preview={previewed}
    >
      <span class="weekday">{words.weekday(day.day)}</span>
      <span class="date">{words.formats.dayOfMonth(day.day)}</span>
      <span class="names">{namesOn(day).join(', ')}</span>
    </button>
  );
}

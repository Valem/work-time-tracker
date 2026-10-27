import type { DayRow } from '../bridge/contract';
import { useAnswer } from '../bridge/useAnswer';
import { useWords } from '../locale/context';
import { Pending } from './Notice';

interface WeekScreenProps {
  /** The week's Monday as yyyy-MM-dd, or none for the current week. */
  readonly monday: string | null;
  readonly tick: number;
  readonly onShowWeek: (monday: string) => void;
  readonly onOpenDay: (date: string) => void;
}

/** Monday to Sunday with start, end, break, total and the rebuilt mark, the week total, and the way to the weeks around it. */
export function WeekScreen({ monday, tick, onShowWeek, onOpenDay }: WeekScreenProps) {
  const words = useWords();
  const { language, formats } = words;
  const answer = useAnswer('week.get', { monday }, monday ?? 'current', tick);
  if (answer.kind !== 'answered') {
    return <Pending answer={answer} />;
  }

  const week = answer.result;
  const first = week.days[0];
  const last = week.days[week.days.length - 1];
  const heading =
    first && last ? language.week.heading(formats.week(week.week), words.day(first.day), words.day(last.day)) : '';
  return (
    <section class="week" aria-labelledby="week-heading">
      <header class="week-head">
        <button
          type="button"
          class="step"
          disabled={week.previous === null}
          title={language.week.previousHint}
          aria-label={language.week.previousHint}
          onClick={() => week.previous && onShowWeek(week.previous.date)}
        >
          {language.week.previous}
        </button>
        <h1 id="week-heading">{heading}</h1>
        <button
          type="button"
          class="step"
          disabled={week.next === null}
          title={language.week.nextHint}
          aria-label={language.week.nextHint}
          onClick={() => week.next && onShowWeek(week.next.date)}
        >
          {language.week.next}
        </button>
      </header>
      <ol class="days">
        {week.days.map((row) => (
          <li key={row.day.date}>
            <WeekRow row={row} onOpen={() => onOpenDay(row.day.date)} />
          </li>
        ))}
      </ol>
      <p class="week-total">{language.week.total(formats.duration(week.total))}</p>
    </section>
  );
}

function WeekRow({ row, onOpen }: { row: DayRow; onOpen: () => void }) {
  const words = useWords();
  const { language, formats } = words;
  const used = row.start !== null && row.end !== null;
  const label = words.day(row.day);
  return (
    <button type="button" class="day-row" data-used={used} title={language.week.openDay(label)} onClick={onOpen}>
      <span class="cell day-label">{label}</span>
      <span class="cell span">
        {row.start !== null && row.end !== null
          ? language.span(formats.time(row.start), formats.time(row.end))
          : language.empty}
      </span>
      <span class="cell pause">{used ? language.week.pause(formats.duration(row.break)) : language.empty}</span>
      <span class="cell total">{formats.duration(row.total)}</span>
      <span class="cell mark">
        {row.rebuilt && (
          <span class="rebuilt" title={language.week.rebuiltHint}>
            {language.week.rebuilt}
          </span>
        )}
      </span>
    </button>
  );
}

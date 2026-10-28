import type { JSX } from 'preact';
import { useState } from 'preact/hooks';
import type { DayView, PauseRole, PauseView } from '../bridge/contract';
import { useAnswer } from '../bridge/useAnswer';
import { useWords, type Words } from '../locale/context';
import { Pending } from './Notice';

/** Where the hour marks of the 00:00–24:00 timeline sit, in minutes after midnight. */
const HourMarks = [0, 180, 360, 540, 720, 900, 1080, 1260, 1440] as const;

interface DayScreenProps {
  readonly date: string;
  readonly tick: number;
  readonly onShowWeek: (monday: string) => void;
}

/** One day: its numbers, a timeline of work and pauses, and why the day reads as it does. */
export function DayScreen({ date, tick, onShowWeek }: DayScreenProps) {
  const words = useWords();
  const { language, formats } = words;
  const answer = useAnswer('day.get', { date }, date, tick);
  if (answer.kind !== 'answered') {
    return <Pending answer={answer} />;
  }

  const day = answer.result;
  return (
    <section class="day" aria-labelledby="day-heading">
      <header class="day-head">
        <button type="button" class="back" onClick={() => onShowWeek(day.monday.date)}>
          {language.day.back}
        </button>
        <h1 id="day-heading">{words.day(day.day)}</h1>
        {day.rebuilt && (
          <span class="rebuilt" title={language.week.rebuiltHint}>
            {language.week.rebuilt}
          </span>
        )}
      </header>
      <dl class="figures">
        <Figure term={language.day.start} value={day.start === null ? language.empty : formats.time(day.start)} />
        <Figure term={language.day.end} value={day.end === null ? language.empty : formats.time(day.end)} />
        <Figure term={language.day.break} value={formats.duration(day.break)} />
        <Figure term={language.day.total} value={formats.duration(day.total)} strong />
      </dl>
      {day.start === null && <p class="pending">{language.day.noUse}</p>}
      {(day.start !== null || day.windowsWakes.length > 0) && <DayDetails day={day} words={words} />}
    </section>
  );
}

function Figure({ term, value, strong = false }: { term: string; value: string; strong?: boolean }) {
  return (
    <div class="figure" data-strong={strong}>
      <dt>{term}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function DayDetails({ day, words }: { day: DayView; words: Words }) {
  return (
    <>
      <Timeline day={day} words={words} />
      <Explanation day={day} words={words} />
    </>
  );
}

/** Why the day reads as it does; closed until the person opens it, each time a day opens. */
function Explanation({ day, words }: { day: DayView; words: Words }) {
  const { language } = words;
  const [open, setOpen] = useState(false);
  return (
    <>
      <h2>
        <button
          type="button"
          class="disclose"
          aria-expanded={open}
          aria-controls="day-explained"
          onClick={() => setOpen(!open)}
        >
          {language.day.explained}
        </button>
      </h2>
      <ul id="day-explained" class="explained" hidden={!open}>
        {day.pauses.map((pause) => (
          <li key={pause.start} data-role={pause.role}>
            {explain(pause, day, words)}
          </li>
        ))}
        {day.start !== null && day.lunch.pause === null && (
          <li data-role="lunch">
            {day.lunch.basis === 'tierMinimum'
              ? language.day.lunchMinimumOnly(words.formats.duration(day.lunch.deducted))
              : language.day.noBreak}
          </li>
        )}
        {day.windowsWakes.length > 0 && (
          <li class="wake-note">{language.day.windowsWakes(day.windowsWakes.map(words.formats.time))}</li>
        )}
      </ul>
    </>
  );
}

function explain(pause: PauseView, day: DayView, words: Words): string {
  const { language, formats } = words;
  const span = language.span(formats.time(pause.start), formats.time(pause.end));
  const duration = formats.duration(pause.minutes);
  switch (pause.role) {
    case 'night':
      return language.day.nightPause(span, duration);
    case 'lunch':
      return day.lunch.basis === 'tierMinimum'
        ? language.day.lunchRaised(span, duration, formats.duration(day.lunch.deducted))
        : language.day.lunchWhole(span, duration);
    case 'evening':
      return language.day.eveningPause(span, duration, formats.duration(day.eveningUse?.minutes ?? 0));
    case 'work':
      return language.day.pauseAsWork(span, duration);
  }
}

/** A stretch placed on the timeline; the stylesheet turns the minutes into a position. */
function placed(start: number, end: number): JSX.CSSProperties {
  return { '--from': String(start), '--to': String(end) };
}

function Timeline({ day, words }: { day: DayView; words: Words }) {
  const { language, formats } = words;
  const legend = legendOf(day, words);
  return (
    <figure class="timeline">
      <figcaption>{language.day.timeline}</figcaption>
      <div class="track">
        {day.blocks.map((block) => (
          <span
            key={`block-${block.start}`}
            class="segment work"
            style={placed(block.start, block.end)}
            title={`${language.day.work} ${words.span(block)}`}
          />
        ))}
        {day.pauses.map((pause) => (
          <span
            key={`pause-${pause.start}`}
            class={`segment pause-${pause.role}`}
            style={placed(pause.start, pause.end)}
            title={`${language.day.roles[pause.role]} ${language.span(formats.time(pause.start), formats.time(pause.end))}`}
          />
        ))}
        {day.eveningUse && (
          <span
            class="segment evening-use"
            style={placed(day.eveningUse.start, day.eveningUse.end)}
            title={`${language.day.eveningUse} ${words.span(day.eveningUse)}`}
          />
        )}
        {day.windowsWakes.map((wake) => (
          <span
            key={`wake-${wake}`}
            class="wake"
            style={{ '--at': String(wake) }}
            title={`${language.day.ignoredWake} ${formats.time(wake)}`}
          />
        ))}
      </div>
      <div class="hours" aria-hidden="true">
        {HourMarks.map((mark) => (
          <span key={mark} style={{ '--at': String(mark) }}>
            {formats.time(mark)}
          </span>
        ))}
      </div>
      {legend.length > 0 && (
        <ul class="legend">
          {legend.map((entry) => (
            <li key={entry.kind} class={`legend-${entry.kind}`}>
              {language.day.legendEntry(entry.label, entry.times)}
            </li>
          ))}
        </ul>
      )}
    </figure>
  );
}

/** One kind on the timeline, named as in the stylesheet, with the formatted span or time of each place it shows. */
interface LegendEntry {
  readonly kind: string;
  readonly label: string;
  readonly times: readonly string[];
}

/** The kinds that occur on the day, in the legend's order, each with its spans in the order the host sent them. */
function legendOf(day: DayView, words: Words): LegendEntry[] {
  const { language, formats } = words;
  const pausesAs = (role: PauseRole): string[] => day.pauses.filter((pause) => pause.role === role).map(words.span);
  const entries: LegendEntry[] = [
    { kind: 'work', label: language.day.work, times: day.workSpans.map(words.span) },
    { kind: 'pause-work', label: language.day.roles.work, times: pausesAs('work') },
    { kind: 'pause-lunch', label: language.day.roles.lunch, times: pausesAs('lunch') },
    { kind: 'pause-night', label: language.day.roles.night, times: pausesAs('night') },
    { kind: 'pause-evening', label: language.day.roles.evening, times: pausesAs('evening') },
    { kind: 'evening-use', label: language.day.eveningUse, times: day.eveningUse ? [words.span(day.eveningUse)] : [] },
    { kind: 'wake', label: language.day.ignoredWake, times: day.windowsWakes.map(formats.time) },
  ];
  return entries.filter((entry) => entry.times.length > 0);
}

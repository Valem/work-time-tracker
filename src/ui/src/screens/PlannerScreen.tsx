import { useEffect, useRef, useState } from 'preact/hooks';
import { ask, refusalCodeOf } from '../bridge/client';
import type { EditView, MonthLayout, Part, PreviewView, RefusalCode, RestoreRequest, YearCounts, YearView } from '../bridge/contract';
import { useWords, type Words } from '../locale/context';
import { LoadLine } from './LoadLine';
import { MyYear } from './MyYear';
import { Pending } from './Notice';
import { usePlanner, type Changed } from './usePlanner';
import { YearGrid } from './YearGrid';

/** How long the message after a change stays, unless the pointer or the focus rests on it. */
const ToastShown = 8_000;

interface Toast {
  readonly text: string;
  readonly refused: boolean;
  /** The request that reverses the change the message names; null where there is nothing to undo. */
  readonly reverse: RestoreRequest | null;
}

function editText(edit: EditView, words: Words): string {
  const { planner } = words.language;
  const { formats } = words;
  const days = planner.days(formats.halfDaysAmount(edit.change), edit.change);
  const from = edit.from;
  const to = edit.to ?? from;
  if (from === null || to === null) {
    return '';
  }

  switch (edit.outcome) {
    case 'booked':
      return planner.edits.booked(words.dates(from, to), days);
    case 'removed':
      return planner.edits.removed(words.dates(from, to), days);
    case 'bookedWhole':
      return planner.edits.bookedWhole(formats.date(from));
    case 'bookedHalf':
      return planner.edits.bookedHalf(formats.date(from));
    case 'ticked':
      return planner.edits.ticked(edit.holiday ?? words.language.empty, formats.date(from));
    case 'unticked':
      return planner.edits.unticked(edit.holiday ?? words.language.empty, formats.date(from));
    case 'nothing':
      return '';
  }
}

/**
 * The «Ferien» screen: the year with its counts always in view, the year switch, the layout, «Mein Jahr», the load state
 * of the public holidays, the year grid and its legend, and a message after each change with the way to undo the last.
 * Only the request that reverses the last change is kept; a saved «Mein Jahr», a reload or another year drops it.
 */
export function PlannerScreen({ tick }: { tick: number }) {
  const words = useWords();
  const { language } = words;
  const [year, setYear] = useState<number | null>(null);
  const planner = usePlanner(year, tick);
  const [preview, setPreview] = useState<PreviewView | null>(null);
  const [toast, setToast] = useState<Toast | null>(null);
  const [myYearOpen, setMyYearOpen] = useState(false);
  const [revision, setRevision] = useState(0);
  const drag = useRef(0);
  const previewsSent = useRef(0);
  const previewShown = useRef(0);

  const { planned } = planner;
  if (planned.kind !== 'answered') {
    return <Pending answer={planned} />;
  }

  const view = planned.view;
  const endDrag = (): void => {
    drag.current++;
    setPreview(null);
  };

  const showYear = (other: number | null): void => {
    if (other !== null) {
      endDrag();
      setYear(other);
      setToast(null);
    }
  };

  const refused = (code: RefusalCode): void => setToast({ text: language.refusals[code], refused: true, reverse: null });

  const edited = (answer: Changed<EditView>): void => {
    if (answer === null) {
      return;
    }

    setRevision((value) => value + 1);
    if (typeof answer === 'string') {
      refused(answer);
    } else if (answer.outcome !== 'nothing') {
      setToast({ text: editText(answer, words), refused: false, reverse: answer.reverse });
    }
  };

  const sendEdit = (send: () => Promise<EditView>): void => {
    planner.change(send, (answer) => answer.year).then(edited);
  };

  const previewTo = (anchor: string, to: string): void => {
    const current = drag.current;
    const sent = ++previewsSent.current;
    ask('planner.preview', { year: view.year, anchor, to }).then(
      (answer) => {
        if (current === drag.current && sent > previewShown.current) {
          previewShown.current = sent;
          setPreview(answer);
        }
      },
      () => undefined,
    );
  };

  const undo = (reverse: RestoreRequest): void => {
    setToast(null);
    planner.change(() => ask('planner.restore', reverse), (answer) => answer).then((answer) => {
      if (answer === null) {
        return;
      }

      setRevision((value) => value + 1);
      if (typeof answer === 'string') {
        refused(answer);
      } else {
        setToast({ text: language.planner.undone, refused: false, reverse: null });
      }
    });
  };

  const tickHoliday = (date: string, share: Part, ticked: boolean): void =>
    sendEdit(() => ask('planner.tick', { year: view.year, date, share, ticked }));

  const changedOutside = (): void => {
    setToast(null);
    setRevision((value) => value + 1);
    planner.refresh();
  };

  const chooseLayout = (layout: MonthLayout): void => {
    ask('planner.layout.save', { layout }).then(
      (answer) => planner.showLayout(answer.layout),
      (failure: unknown) => refused(refusalCodeOf(failure)),
    );
  };

  return (
    <section class="planner" aria-labelledby="planner-year">
      <CountsBar
        view={view}
        preview={preview}
        myYearOpen={myYearOpen}
        onYear={showYear}
        onLayout={chooseLayout}
        onMyYear={() => setMyYearOpen((open) => !open)}
      />
      <div class="planner-body">
        <LoadLine load={view.load} />
        {myYearOpen && (
          <MyYear
            year={view.year}
            revision={revision}
            load={view.load}
            onClose={() => setMyYearOpen(false)}
            onChanged={changedOutside}
            onTick={tickHoliday}
          />
        )}
        <YearGrid
          view={view}
          preview={preview}
          onPreview={previewTo}
          onDrag={(anchor, to) => {
            endDrag();
            sendEdit(() => ask('planner.drag', { year: view.year, anchor, to }));
          }}
          onDragDropped={endDrag}
          onClick={(date) => {
            endDrag();
            sendEdit(() => ask('planner.click', { year: view.year, date }));
          }}
        />
        <Legend words={words} />
      </div>
      {toast !== null && <ToastMessage toast={toast} onUndo={undo} onGone={() => setToast(null)} />}
    </section>
  );
}

interface CountsBarProps {
  readonly view: YearView;
  readonly preview: PreviewView | null;
  readonly myYearOpen: boolean;
  readonly onYear: (year: number | null) => void;
  readonly onLayout: (layout: MonthLayout) => void;
  readonly onMyYear: () => void;
}

/** The year switch, yearly, taken, planned and left with their meter, the live change while dragging, the layout and «Mein Jahr». */
function CountsBar({ view, preview, myYearOpen, onYear, onLayout, onMyYear }: CountsBarProps) {
  const words = useWords();
  const { language, formats } = words;
  const { planner } = language;
  const counts: YearCounts = view.counts;
  return (
    <header class="counts-bar">
      <div class="year-switch">
        <button
          type="button"
          class="step"
          disabled={view.previous === null}
          title={view.previous === null ? undefined : planner.previousYear(formats.year(view.previous))}
          aria-label={view.previous === null ? undefined : planner.previousYear(formats.year(view.previous))}
          onClick={() => onYear(view.previous)}
        >
          {language.week.previous}
        </button>
        <h1 id="planner-year">{formats.year(view.year)}</h1>
        <button
          type="button"
          class="step"
          disabled={view.next === null}
          title={view.next === null ? undefined : planner.nextYear(formats.year(view.next))}
          aria-label={view.next === null ? undefined : planner.nextYear(formats.year(view.next))}
          onClick={() => onYear(view.next)}
        >
          {language.week.next}
        </button>
      </div>
      <dl class="counts">
        <Figure label={planner.yearly} value={formats.halfDays(counts.yearly)} />
        <Figure label={planner.taken} value={formats.halfDays(counts.taken)} />
        <Figure label={planner.planned} value={formats.halfDays(counts.planned)} />
        <Figure label={planner.left} value={formats.halfDays(counts.left)} strong negative={counts.left < 0} />
      </dl>
      <div
        class="meter"
        role="img"
        aria-label={planner.meter(formats.halfDays(counts.taken), formats.halfDays(counts.planned), formats.halfDays(counts.yearly))}
      >
        <i class="taken" style={{ '--permille': String(counts.takenPermille) }} />
        <i class="planned" style={{ '--permille': String(counts.plannedPermille) }} />
      </div>
      <p class="live" role="status">
        {preview !== null &&
          planner.live(
            words.dates(preview.from, preview.to),
            formats.signedHalfDays(preview.change),
            formats.halfDays(preview.countsAfter.left),
          )}
      </p>
      <div class="tools">
        <div class="segmented" role="group" aria-label={planner.layout}>
          <button type="button" aria-pressed={view.layout === 'columns'} onClick={() => onLayout('columns')}>
            {planner.columns}
          </button>
          <button type="button" aria-pressed={view.layout === 'rows'} onClick={() => onLayout('rows')}>
            {planner.rows}
          </button>
        </div>
        <button type="button" class="quiet" aria-expanded={myYearOpen} onClick={onMyYear}>
          {planner.myYear}
        </button>
      </div>
    </header>
  );
}

function Figure({ label, value, strong = false, negative = false }: { label: string; value: string; strong?: boolean; negative?: boolean }) {
  return (
    <div class="count" data-strong={strong || undefined} data-negative={negative || undefined}>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function Legend({ words }: { words: Words }) {
  const { legend } = words.language.planner;
  return (
    <ul class="plan-legend">
      <li class="holiday">{legend.holiday}</li>
      <li class="half-holiday">{legend.halfHoliday}</li>
      <li class="public-holiday">{legend.publicHoliday}</li>
      <li class="offered">{legend.offered}</li>
      <li class="free-day">{legend.freeDay}</li>
      <li class="weekend">{legend.weekend}</li>
      <li class="bridge">{legend.bridge}</li>
    </ul>
  );
}

interface ToastProps {
  readonly toast: Toast;
  readonly onUndo: (reverse: RestoreRequest) => void;
  readonly onGone: () => void;
}

/** The message after a change, with «Rückgängig» for the last one; it goes after a while unless the pointer or focus rests on it. */
function ToastMessage({ toast, onUndo, onGone }: ToastProps) {
  const { language } = useWords();
  const [resting, setResting] = useState(false);
  const gone = useRef(onGone);
  gone.current = onGone;

  useEffect(() => {
    if (resting) {
      return undefined;
    }

    const timer = window.setTimeout(() => gone.current(), ToastShown);
    return () => window.clearTimeout(timer);
  }, [toast, resting]);

  return (
    <div
      class="toast"
      data-refused={toast.refused || undefined}
      role={toast.refused ? 'alert' : 'status'}
      onPointerEnter={() => setResting(true)}
      onPointerLeave={() => setResting(false)}
      onFocusIn={() => setResting(true)}
      onFocusOut={() => setResting(false)}
    >
      <span>{toast.text}</span>
      {toast.reverse !== null && (
        <button type="button" class="undo" onClick={() => toast.reverse !== null && onUndo(toast.reverse)}>
          {language.planner.undo}
        </button>
      )}
    </div>
  );
}

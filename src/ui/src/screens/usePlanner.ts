import { useEffect, useRef, useState } from 'preact/hooks';
import { ask, refusalCodeOf } from '../bridge/client';
import type { MonthLayout, RefusalCode, YearView } from '../bridge/contract';

/** How often the year is asked for again while its public holidays are loading. */
const LoadingRefresh = 2_000;

export type Planned =
  | { readonly kind: 'waiting' }
  | { readonly kind: 'answered'; readonly view: YearView }
  | { readonly kind: 'refused'; readonly code: RefusalCode };

export interface Planner {
  readonly planned: Planned;
  /**
   * Sends a change — a gesture, a tick, a restore — after every change sent before it has been answered, and shows the
   * year it answers with. Resolves with the answer, or with the refusal's code.
   */
  readonly change: <T>(send: () => Promise<T>, yearOf: (answer: T) => YearView) => Promise<T | RefusalCode>;
  /** Asks for the year again, as after a saved «Mein Jahr» or a reload. */
  readonly refresh: () => void;
  readonly showLayout: (layout: MonthLayout) => void;
}

/**
 * One year of the planner as the host last answered it. Every request takes a number when it is sent, and an answer is
 * shown only if no later request's answer is shown already, so a refresh sent before a change never shows the year as it
 * was before that change. No refresh is sent while a change waits for its answer.
 */
export function usePlanner(year: number | null, tick: number): Planner {
  const [planned, setPlanned] = useState<Planned>({ kind: 'waiting' });
  const sent = useRef(0);
  const shown = useRef(0);
  const changing = useRef(0);
  const queue = useRef<Promise<unknown>>(Promise.resolve());

  const show = (number: number, next: Planned): void => {
    if (number > shown.current) {
      shown.current = number;
      setPlanned(next);
    }
  };

  const askYear = (): void => {
    if (changing.current > 0) {
      return;
    }

    const number = ++sent.current;
    ask('planner.year.get', { year }).then(
      (view) => show(number, { kind: 'answered', view }),
      (failure: unknown) => show(number, { kind: 'refused', code: refusalCodeOf(failure) }),
    );
  };

  useEffect(() => {
    shown.current = sent.current;
    setPlanned({ kind: 'waiting' });
  }, [year]);

  useEffect(askYear, [year, tick]);

  const loading = planned.kind === 'answered' && planned.view.load.status === 'loading';
  useEffect(() => {
    if (!loading) {
      return undefined;
    }

    const timer = window.setInterval(askYear, LoadingRefresh);
    return () => window.clearInterval(timer);
  }, [loading, year]);

  const change = <T>(send: () => Promise<T>, yearOf: (answer: T) => YearView): Promise<T | RefusalCode> => {
    changing.current++;
    const run = queue.current.then(async () => {
      const number = ++sent.current;
      try {
        const answer = await send();
        show(number, { kind: 'answered', view: yearOf(answer) });
        return answer;
      } catch (failure: unknown) {
        return refusalCodeOf(failure);
      } finally {
        changing.current--;
      }
    });
    queue.current = run;
    return run;
  };

  const showLayout = (layout: MonthLayout): void =>
    setPlanned((current) => (current.kind === 'answered' ? { kind: 'answered', view: { ...current.view, layout } } : current));

  return { planned, change, refresh: askYear, showLayout };
}

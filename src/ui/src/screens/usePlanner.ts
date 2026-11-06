import { useEffect, useRef, useState } from 'preact/hooks';
import { ask, refusalCodeOf } from '../bridge/client';
import type { MonthLayout, RefusalCode, YearView } from '../bridge/contract';

/** How often the year is asked for again while its public holidays are loading. */
const LoadingRefresh = 2_000;

export type Planned =
  | { readonly kind: 'waiting' }
  | { readonly kind: 'answered'; readonly view: YearView }
  | { readonly kind: 'refused'; readonly code: RefusalCode };

/** A change's answer, or its refusal's code; null when another year has been chosen since the change was made. */
export type Changed<T> = T | RefusalCode | null;

export interface Planner {
  readonly planned: Planned;
  /**
   * Sends a change — a gesture, a tick, a restore — after every change sent before it has been answered, and shows the
   * year it answers with, unless another year has been chosen since.
   */
  readonly change: <T>(send: () => Promise<T>, yearOf: (answer: T) => YearView) => Promise<Changed<T>>;
  /** Asks for the year again, as after a saved «Mein Jahr» or a reload. */
  readonly refresh: () => void;
  readonly showLayout: (layout: MonthLayout) => void;
}

/**
 * One year of the planner as the host last answered it. Each choice of a year opens a new turn: a request belongs to the
 * turn it was made in, and its answer shows only in that turn and only if no later request's answer shows already — so
 * neither a refresh sent before a change nor anything asked for another year ever shows. No refresh goes while a change
 * waits for its answer; one asked for meanwhile goes once the changes have settled.
 */
export function usePlanner(year: number | null, tick: number): Planner {
  const [planned, setPlanned] = useState<Planned>({ kind: 'waiting' });
  const chosen = useRef<{ readonly year: number | null; readonly turn: number } | null>(null);
  const sent = useRef(0);
  const shown = useRef(0);
  const changing = useRef(0);
  const askWhenSettled = useRef(false);
  const queue = useRef<Promise<unknown>>(Promise.resolve());

  const turn = (): number => chosen.current?.turn ?? 0;

  const show = (madeIn: number, number: number, next: Planned): void => {
    if (madeIn === turn() && number > shown.current) {
      shown.current = number;
      setPlanned(next);
    }
  };

  const askYear = (): void => {
    if (changing.current > 0) {
      askWhenSettled.current = true;
      return;
    }

    const madeIn = turn();
    const number = ++sent.current;
    ask('planner.year.get', { year: chosen.current?.year ?? null }).then(
      (view) => show(madeIn, number, { kind: 'answered', view }),
      (failure: unknown) => show(madeIn, number, { kind: 'refused', code: refusalCodeOf(failure) }),
    );
  };

  useEffect(() => {
    if (chosen.current === null || chosen.current.year !== year) {
      chosen.current = { year, turn: turn() + 1 };
      setPlanned({ kind: 'waiting' });
    }

    askYear();
  }, [year, tick]);

  const loading = planned.kind === 'answered' && planned.view.load.status === 'loading';
  useEffect(() => {
    if (!loading) {
      return undefined;
    }

    const timer = window.setInterval(askYear, LoadingRefresh);
    return () => window.clearInterval(timer);
  }, [loading, year]);

  const change = <T>(send: () => Promise<T>, yearOf: (answer: T) => YearView): Promise<Changed<T>> => {
    const madeIn = turn();
    changing.current++;
    const run = queue.current.then(async (): Promise<Changed<T>> => {
      const number = ++sent.current;
      try {
        const answer = await send();
        show(madeIn, number, { kind: 'answered', view: yearOf(answer) });
        return madeIn === turn() ? answer : null;
      } catch (failure: unknown) {
        return madeIn === turn() ? refusalCodeOf(failure) : null;
      } finally {
        changing.current--;
        if (changing.current === 0 && askWhenSettled.current) {
          askWhenSettled.current = false;
          askYear();
        }
      }
    });
    queue.current = run;
    return run;
  };

  const showLayout = (layout: MonthLayout): void =>
    setPlanned((current) => (current.kind === 'answered' ? { kind: 'answered', view: { ...current.view, layout } } : current));

  return { planned, change, refresh: askYear, showLayout };
}

import { useEffect, useState } from 'preact/hooks';
import type { RefusalCode } from './contract';
import { ask, refusalCodeOf, type MessageType, type RequestOf, type ResultOf } from './client';

export type Answer<T extends MessageType> =
  | { readonly kind: 'waiting' }
  | { readonly kind: 'answered'; readonly result: ResultOf<T> }
  | { readonly kind: 'refused'; readonly code: RefusalCode };

interface Held<T extends MessageType> {
  readonly key: string;
  readonly answer: Answer<T>;
}

/**
 * Asks the host whenever the key or the refresh tick changes. A refresh keeps showing the last answer until the new one
 * arrives; a new key (another week or day) shows nothing of the old one.
 */
export function useAnswer<T extends MessageType>(type: T, payload: RequestOf<T>, key: string, tick: number): Answer<T> {
  const [held, setHeld] = useState<Held<T>>({ key, answer: { kind: 'waiting' } });

  useEffect(() => {
    let current = true;
    ask(type, payload).then(
      (result) => current && setHeld({ key, answer: { kind: 'answered', result } }),
      (failure: unknown) => current && setHeld({ key, answer: { kind: 'refused', code: refusalCodeOf(failure) } }),
    );
    return () => {
      current = false;
    };
    // The key stands for the payload, which is a new object at every render.
  }, [type, key, tick]);

  return held.key === key ? held.answer : { kind: 'waiting' };
}

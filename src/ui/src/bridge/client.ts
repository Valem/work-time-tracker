import type { BridgeAnswer, BridgeMessages, BridgeRefusal, BridgeRequest, RefusalCode } from './contract';

export type MessageType = keyof BridgeMessages;
export type RequestOf<T extends MessageType> = BridgeMessages[T]['request'];
export type ResultOf<T extends MessageType> = BridgeMessages[T]['result'];

/** The host refused the request with this code; nothing was saved. */
export class Refused extends Error {
  constructor(readonly code: RefusalCode) {
    super(code);
  }
}

export function refusalCodeOf(failure: unknown): RefusalCode {
  return failure instanceof Refused ? failure.code : 'failed';
}

interface Waiting {
  readonly answer: (result: unknown) => void;
  readonly refuse: (refusal: Refused) => void;
}

const waiting = new Map<number, Waiting>();
let lastId = 0;
let listening = false;

function listenTo(host: HostWebView): void {
  if (listening) {
    return;
  }

  listening = true;
  host.addEventListener('message', ({ data }) => {
    const reply = data as BridgeAnswer | BridgeRefusal;
    const request = reply.id === null ? undefined : waiting.get(reply.id);
    if (request === undefined) {
      return;
    }

    waiting.delete(reply.id as number);
    if ('error' in reply) {
      request.refuse(new Refused(reply.error.code));
    } else {
      request.answer(reply.result);
    }
  });
}

/** Asks the host; the UI asks and the host answers, it never pushes. */
export function ask<T extends MessageType>(type: T, payload: RequestOf<T>): Promise<ResultOf<T>> {
  const host = window.chrome?.webview;
  if (host === undefined) {
    return Promise.reject(new Refused('failed'));
  }

  listenTo(host);
  const id = ++lastId;
  const request: BridgeRequest = { id, type, payload };
  return new Promise<ResultOf<T>>((resolve, reject) => {
    waiting.set(id, { answer: (result) => resolve(result as ResultOf<T>), refuse: reject });
    host.postMessage(request);
  });
}

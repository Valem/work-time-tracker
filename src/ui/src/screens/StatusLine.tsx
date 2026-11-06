import { useAnswer } from '../bridge/useAnswer';
import { useWords } from '../locale/context';

/** Whether the Recorder records and when it last did, or since when it has stopped; always in view. */
export function StatusLine({ tick }: { tick: number }) {
  const words = useWords();
  const { status } = words.language;
  const answer = useAnswer('status.get', {}, 'status', tick);
  if (answer.kind !== 'answered') {
    return <p class="status" data-state="unknown" role="status" />;
  }

  const { recording, lastRecorded } = answer.result;
  const state = recording ? 'recording' : lastRecorded === null ? 'never' : 'stopped';
  const text =
    lastRecorded === null
      ? status.never
      : recording
        ? status.recording(words.reading(lastRecorded))
        : status.stopped(words.reading(lastRecorded));
  return (
    <p class="status" data-state={state} role="status">
      <span class="status-dot" aria-hidden="true" />
      {text}
    </p>
  );
}

import type { MessageType } from '../bridge/client';
import type { Answer } from '../bridge/useAnswer';
import { useWords } from '../locale/context';

/** What a screen shows until its answer is there: a wait, or the reason the host refused. */
export function Pending<T extends MessageType>({ answer }: { answer: Exclude<Answer<T>, { kind: 'answered' }> }) {
  const { language } = useWords();
  return answer.kind === 'waiting' ? (
    <p class="pending">{language.loading}</p>
  ) : (
    <p class="refusal" role="alert">
      {language.refusals[answer.code]}
    </p>
  );
}

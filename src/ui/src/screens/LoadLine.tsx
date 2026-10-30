import type { HolidayLoadView } from '../bridge/contract';
import { useWords } from '../locale/context';

/** Whether the canton's public holidays of the year are loaded: since when, or why not and that the counts may change. */
export function LoadLine({ load }: { load: HolidayLoadView }) {
  const words = useWords();
  const { loads } = words.language;
  const text = (() => {
    switch (load.status) {
      case 'noCanton':
        return loads.noCanton;
      case 'loading':
        return loads.loading;
      case 'loaded':
        return loads.loaded(load.loadedOn === null ? words.language.empty : words.formats.date(load.loadedOn));
      case 'notLoaded':
        return load.reason === null ? loads.notLoaded : loads.notLoadedBecause(loads.reasons[load.reason]);
      case 'outOfRange':
        return loads.outOfRange;
    }
  })();
  return (
    <p class="load-line" data-status={load.status} role="status">
      {text}
    </p>
  );
}

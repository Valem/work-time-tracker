import { useState } from 'preact/hooks';
import { WordsContext, wordsOf } from './locale/context';
import type { Locale } from './locale/types';
import { useRefreshTick } from './refresh';
import { DayScreen } from './screens/DayScreen';
import { PlannerScreen } from './screens/PlannerScreen';
import { SettingsScreen } from './screens/SettingsScreen';
import { StatusLine } from './screens/StatusLine';
import { WeekScreen } from './screens/WeekScreen';

type Screen =
  | { readonly kind: 'week'; readonly monday: string | null }
  | { readonly kind: 'day'; readonly date: string }
  | { readonly kind: 'planner' }
  | { readonly kind: 'settings' };

/** One window: the recording status always in view, and the week, a day, the year planner or the settings below it. */
export function App({ locale }: { locale: Locale }) {
  const [words] = useState(() => wordsOf(locale));
  const [screen, setScreen] = useState<Screen>({ kind: 'week', monday: null });
  const tick = useRefreshTick();
  const { language } = words;
  const showWeek = (monday: string | null): void => setScreen({ kind: 'week', monday });
  const onWeek = screen.kind === 'week' || screen.kind === 'day';

  return (
    <WordsContext.Provider value={words}>
      <header class="bar">
        <span class="product">{language.productName}</span>
        <nav aria-label={language.nav.label}>
          <button type="button" aria-current={onWeek} onClick={() => showWeek(null)}>
            {language.nav.week}
          </button>
          <button type="button" aria-current={screen.kind === 'planner'} onClick={() => setScreen({ kind: 'planner' })}>
            {language.nav.planner}
          </button>
          <button type="button" aria-current={screen.kind === 'settings'} onClick={() => setScreen({ kind: 'settings' })}>
            {language.nav.settings}
          </button>
        </nav>
        <StatusLine tick={tick} />
      </header>
      <div class="scroller">
        <main data-wide={screen.kind === 'planner' || undefined}>
          {screen.kind === 'week' && (
            <WeekScreen
              monday={screen.monday}
              tick={tick}
              onShowWeek={showWeek}
              onOpenDay={(date) => setScreen({ kind: 'day', date })}
            />
          )}
          {screen.kind === 'day' && <DayScreen date={screen.date} tick={tick} onShowWeek={showWeek} />}
          {screen.kind === 'planner' && <PlannerScreen tick={tick} />}
          {screen.kind === 'settings' && <SettingsScreen />}
        </main>
      </div>
    </WordsContext.Provider>
  );
}

import { useEffect, useState } from 'preact/hooks';
import { ask, refusalCodeOf } from '../bridge/client';
import type {
  Canton,
  HolidayLoadView,
  Part,
  PlannerSettingsInput,
  PlannerSettingsView,
  RefusalCode,
  WeekdayUse,
} from '../bridge/contract';
import { useWords, type Words } from '../locale/context';
import { LoadLine } from './LoadLine';

interface MyYearProps {
  readonly year: number;
  /** Moves whenever the year's plan changed, so the ticks shown follow it. */
  readonly revision: number;
  /** The load state of the year's public holidays, as the year screen last heard it; the entries to tick follow it. */
  readonly load: HolidayLoadView;
  readonly onClose: () => void;
  /** «Mein Jahr» was saved or the public holidays reloaded: the year is asked for again and the undo dropped. */
  readonly onChanged: () => void;
  /** «Mein Jahr»'s tick box, sent through the planner as an edit. */
  readonly onTick: (date: string, share: Part, ticked: boolean) => void;
}

interface Draft {
  readonly pensum: string;
  readonly weekdays: readonly WeekdayUse[];
  readonly yearlyDays: string;
  readonly canton: Canton | null;
}

type Outcome =
  | { readonly kind: 'saved' }
  | { readonly kind: 'refused'; readonly code: RefusalCode }
  | { readonly kind: 'unreadable' };

function draftOf(settings: PlannerSettingsView, words: Words): Draft {
  return {
    pensum: String(settings.pensum),
    weekdays: settings.weekdays,
    yearlyDays: settings.yearlyDays === null ? '' : words.formats.halfDays(settings.yearlyDays),
    canton: settings.canton,
  };
}

/** The draft as the host reads it, or null when the Pensum or the yearly days do not read as numbers. */
function inputOf(year: number, draft: Draft, words: Words): PlannerSettingsInput | null {
  const pensum = words.formats.readWhole(draft.pensum);
  const yearlyDays = draft.yearlyDays.trim() === '' ? null : words.formats.readHalfDays(draft.yearlyDays);
  if (pensum === null || (draft.yearlyDays.trim() !== '' && yearlyDays === null)) {
    return null;
  }

  return { year, pensum, weekdays: [...draft.weekdays], yearlyDays, canton: draft.canton };
}

/**
 * «Mein Jahr» of the shown year: Pensum, each weekday working or free, yearly holiday days (empty: prefilled), canton,
 * the public holidays to tick, whether they are loaded with the way to load them again, and their source.
 */
export function MyYear({ year, revision, load, onClose, onChanged, onTick }: MyYearProps) {
  const words = useWords();
  const { language, formats } = words;
  const texts = language.myYear;
  const [settings, setSettings] = useState<PlannerSettingsView | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [failure, setFailure] = useState<RefusalCode | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [saving, setSaving] = useState(false);
  const [reloading, setReloading] = useState(false);

  useEffect(() => {
    setDraft(null);
    setOutcome(null);
  }, [year]);

  useEffect(() => {
    let current = true;
    ask('planner.settings.get', { year }).then(
      (answer) => {
        if (current) {
          setSettings(answer);
          setFailure(null);
          setDraft((kept) => kept ?? draftOf(answer, words));
        }
      },
      (refused: unknown) => current && setFailure(refusalCodeOf(refused)),
    );
    return () => {
      current = false;
    };
  }, [year, revision, load.status, load.loadedOn?.date]);

  if (settings === null || draft === null) {
    return (
      <section class="my-year card">
        <p class={failure === null ? 'pending' : 'refusal'}>{failure === null ? language.loading : language.refusals[failure]}</p>
      </section>
    );
  }

  const edit = (change: Partial<Draft>): void => setDraft((current) => (current === null ? current : { ...current, ...change }));

  const save = async (event: Event): Promise<void> => {
    event.preventDefault();
    const input = inputOf(year, draft, words);
    if (input === null) {
      setOutcome({ kind: 'unreadable' });
      return;
    }

    setSaving(true);
    try {
      const saved = await ask('planner.settings.save', input);
      setSettings(saved);
      setDraft(draftOf(saved, words));
      setOutcome({ kind: 'saved' });
      onChanged();
    } catch (refused: unknown) {
      setOutcome({ kind: 'refused', code: refusalCodeOf(refused) });
    } finally {
      setSaving(false);
    }
  };

  const reload = async (): Promise<void> => {
    setReloading(true);
    try {
      await ask('planner.reload', { year });
      onChanged();
    } catch (refused: unknown) {
      setOutcome({ kind: 'refused', code: refusalCodeOf(refused) });
    } finally {
      setReloading(false);
    }
  };

  const shown: HolidayLoadView = reloading ? { status: 'loading', loadedOn: null, reason: null } : load;
  const canReload = load.status !== 'noCanton' && load.status !== 'outOfRange';
  return (
    <section class="my-year card" aria-labelledby="my-year-heading">
      <header class="my-year-head">
        <h2 id="my-year-heading">{texts.heading(formats.year(year))}</h2>
        <button type="button" class="quiet" onClick={onClose}>
          {texts.close}
        </button>
      </header>
      <p class="hint">{texts.scope(formats.year(year))}</p>
      <form class="my-year-fields" onSubmit={save} noValidate>
        <label class="field">
          <span>{texts.pensum}</span>
          <span class="with-unit">
            <input
              type="text"
              inputMode="numeric"
              autoComplete="off"
              size={4}
              value={draft.pensum}
              onInput={(event) => edit({ pensum: event.currentTarget.value })}
            />
            {texts.pensumUnit}
          </span>
        </label>
        <fieldset class="field weekdays">
          <legend>{texts.weekdays}</legend>
          <div class="toggles">
            {draft.weekdays.map((use, index) => (
              <button
                type="button"
                key={index}
                class="toggle"
                aria-pressed={use === 'free'}
                onClick={() =>
                  edit({ weekdays: draft.weekdays.map((other, at) => (at === index ? (other === 'free' ? 'working' : 'free') : other)) })
                }
              >
                <span>{language.weekdays[index]}</span>
                <small>{use === 'free' ? texts.free : texts.working}</small>
              </button>
            ))}
          </div>
        </fieldset>
        <label class="field">
          <span>{texts.yearlyDays}</span>
          <input
            type="text"
            inputMode="decimal"
            autoComplete="off"
            size={5}
            value={draft.yearlyDays}
            placeholder={formats.halfDays(settings.prefilledYearlyDays)}
            onInput={(event) => edit({ yearlyDays: event.currentTarget.value })}
          />
          <small class="hint">{texts.prefilled(formats.halfDays(settings.prefilledYearlyDays))}</small>
        </label>
        <label class="field">
          <span>{texts.canton}</span>
          <select
            value={draft.canton ?? ''}
            onChange={(event) => {
              const chosen = settings.cantons.find((canton) => canton === event.currentTarget.value);
              edit({ canton: chosen ?? null });
            }}
          >
            <option value="">{texts.noCanton}</option>
            {settings.cantons.map((canton) => (
              <option key={canton} value={canton}>
                {language.cantons[canton]}
              </option>
            ))}
          </select>
          <small class="hint">{texts.cantonHint}</small>
        </label>
        <div class="actions">
          <button type="submit" class="primary" disabled={saving}>
            {saving ? texts.saving : texts.save}
          </button>
          <Outcome outcome={outcome} words={words} />
        </div>
      </form>
      <fieldset class="field offered">
        <legend>{texts.offered}</legend>
        {settings.offered.length === 0 ? (
          <p class="hint">{texts.noneOffered}</p>
        ) : (
          <ul>
            {settings.offered.map((holiday) => (
              <li key={`${holiday.day.date}-${holiday.share}`}>
                <label>
                  <input
                    type="checkbox"
                    checked={holiday.ticked}
                    onChange={(event) => onTick(holiday.day.date, holiday.share, event.currentTarget.checked)}
                  />
                  <span>{texts.offeredEntry(holiday.name, words.day(holiday.day), holiday.share === 'half')}</span>
                </label>
              </li>
            ))}
          </ul>
        )}
      </fieldset>
      <div class="load">
        <LoadLine load={shown} />
        <button type="button" class="quiet" disabled={!canReload} aria-busy={reloading} onClick={reload}>
          {reloading ? texts.reloading : texts.reload}
        </button>
      </div>
      <p class="source">{texts.attribution(settings.source.name, settings.source.licence)}</p>
    </section>
  );
}

function Outcome({ outcome, words }: { outcome: Outcome | null; words: Words }) {
  const { language } = words;
  if (outcome === null) {
    return null;
  }

  switch (outcome.kind) {
    case 'saved':
      return (
        <p class="saved" role="status">
          {language.myYear.saved}
        </p>
      );
    case 'refused':
      return (
        <p class="refusal" role="alert">
          {outcome.code === 'valueOutOfRange' ? language.myYear.outOfRange : language.refusals[outcome.code]}
        </p>
      );
    case 'unreadable':
      return (
        <p class="refusal" role="alert">
          {language.myYear.unreadable}
        </p>
      );
  }
}

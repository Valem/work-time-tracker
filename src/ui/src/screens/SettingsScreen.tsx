import { useEffect, useState } from 'preact/hooks';
import { ask, refusalCodeOf } from '../bridge/client';
import type { CalendarDay, LunchRuleInput, LunchRuleView, LunchTierInput, RefusalCode } from '../bridge/contract';
import { useWords, type Words } from '../locale/context';
import type { Formats } from '../locale/types';

interface DraftTier {
  /** Identifies the row while it is edited; never sent. */
  readonly row: number;
  readonly over: string;
  readonly minimum: string;
}

interface Draft {
  readonly start: string;
  readonly end: string;
  readonly tiers: readonly DraftTier[];
}

type Outcome =
  | { readonly kind: 'saved'; readonly validFrom: CalendarDay | null }
  | { readonly kind: 'refused'; readonly code: RefusalCode }
  | { readonly kind: 'unreadable' };

let nextRow = 0;

function draftOf(rule: LunchRuleView, formats: Formats): Draft {
  return {
    start: formats.time(rule.window.start),
    end: formats.time(rule.window.end),
    tiers: rule.tiers.map((tier) => ({
      row: nextRow++,
      over: formats.duration(tier.over),
      minimum: formats.duration(tier.minimum),
    })),
  };
}

/** The draft as the host reads it, or null when a field does not read as hours and minutes. */
function inputOf(draft: Draft, formats: Formats): LunchRuleInput | null {
  const start = formats.readTime(draft.start);
  const end = formats.readTime(draft.end);
  if (start === null || end === null) {
    return null;
  }

  const tiers: LunchTierInput[] = [];
  for (const tier of draft.tiers) {
    const over = formats.readDuration(tier.over);
    const minimum = formats.readDuration(tier.minimum);
    if (over === null || minimum === null) {
      return null;
    }

    tiers.push({ over, minimum });
  }

  return { window: { start, end }, tiers };
}

/** Lunch window and minimum-break tiers: the only setup, prefilled until the first save. */
export function SettingsScreen() {
  const words = useWords();
  const { language, formats } = words;
  const [rule, setRule] = useState<LunchRuleView | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let current = true;
    ask('lunchRule.get', {}).then(
      (loaded) => {
        if (current) {
          setRule(loaded);
          setDraft(draftOf(loaded, formats));
        }
      },
      (failure: unknown) => current && setOutcome({ kind: 'refused', code: refusalCodeOf(failure) }),
    );
    return () => {
      current = false;
    };
  }, [formats]);

  if (draft === null || rule === null) {
    return outcome?.kind === 'refused' ? (
      <p class="refusal" role="alert">
        {language.refusals[outcome.code]}
      </p>
    ) : (
      <p class="pending">{language.loading}</p>
    );
  }

  const save = async (event: Event): Promise<void> => {
    event.preventDefault();
    const input = inputOf(draft, formats);
    if (input === null) {
      setOutcome({ kind: 'unreadable' });
      return;
    }

    setSaving(true);
    try {
      const saved = await ask('lunchRule.save', input);
      setRule(saved);
      setDraft(draftOf(saved, formats));
      setOutcome({ kind: 'saved', validFrom: saved.validFrom });
    } catch (failure: unknown) {
      setOutcome({ kind: 'refused', code: refusalCodeOf(failure) });
    } finally {
      setSaving(false);
    }
  };

  const edit = (change: (current: Draft) => Draft): void =>
    setDraft((current) => (current === null ? current : change(current)));
  const changeTier = (row: number, change: Partial<Pick<DraftTier, 'over' | 'minimum'>>): void =>
    edit((current) => ({
      ...current,
      tiers: current.tiers.map((tier) => (tier.row === row ? { ...tier, ...change } : tier)),
    }));

  return (
    <section class="settings" aria-labelledby="settings-heading">
      <h1 id="settings-heading">{language.settings.heading}</h1>
      <p class="rule-state">
        {rule.prefilled || rule.validFrom === null
          ? language.settings.prefilled
          : language.settings.validFrom(words.day(rule.validFrom))}
      </p>
      <form onSubmit={save} noValidate>
        <fieldset>
          <legend>{language.settings.window}</legend>
          <div class="window-fields">
            <label>
              <span>{language.settings.windowFrom}</span>
              <TimeField value={draft.start} placeholder={formats.timePlaceholder} onInput={(start) => edit((current) => ({ ...current, start }))} />
            </label>
            <label>
              <span>{language.settings.windowTo}</span>
              <TimeField value={draft.end} placeholder={formats.timePlaceholder} onInput={(end) => edit((current) => ({ ...current, end }))} />
            </label>
          </div>
        </fieldset>
        <fieldset>
          <legend>{language.settings.tiers}</legend>
          <p class="hint">{language.settings.tiersHint}</p>
          {draft.tiers.length === 0 && <p class="hint">{language.settings.noTiers}</p>}
          <ol class="tiers">
            {draft.tiers.map((tier) => (
              <li key={tier.row}>
                <label>
                  <span>{language.settings.tierOver}</span>
                  <TimeField value={tier.over} placeholder={formats.durationPlaceholder} onInput={(over) => changeTier(tier.row, { over })} />
                </label>
                <label>
                  <span>{language.settings.tierMinimum}</span>
                  <TimeField
                    value={tier.minimum}
                    placeholder={formats.durationPlaceholder}
                    onInput={(minimum) => changeTier(tier.row, { minimum })}
                  />
                </label>
                <button
                  type="button"
                  class="quiet"
                  onClick={() => edit((current) => ({ ...current, tiers: current.tiers.filter((other) => other.row !== tier.row) }))}
                >
                  {language.settings.removeTier}
                </button>
              </li>
            ))}
          </ol>
          <button
            type="button"
            class="quiet"
            onClick={() => edit((current) => ({ ...current, tiers: [...current.tiers, { row: nextRow++, over: '', minimum: '' }] }))}
          >
            {language.settings.addTier}
          </button>
        </fieldset>
        <div class="actions">
          <button type="submit" class="primary" disabled={saving}>
            {saving ? language.settings.saving : language.settings.save}
          </button>
          <Outcome outcome={outcome} words={words} />
        </div>
      </form>
    </section>
  );
}

function TimeField({ value, placeholder, onInput }: { value: string; placeholder: string; onInput: (value: string) => void }) {
  return (
    <input
      type="text"
      inputMode="numeric"
      autoComplete="off"
      spellcheck={false}
      size={5}
      value={value}
      placeholder={placeholder}
      onInput={(event) => onInput(event.currentTarget.value)}
    />
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
          {outcome.validFrom === null ? language.settings.prefilled : language.settings.saved(words.day(outcome.validFrom))}
        </p>
      );
    case 'refused':
      return (
        <p class="refusal" role="alert">
          {language.refusals[outcome.code]}
        </p>
      );
    case 'unreadable':
      return (
        <p class="refusal" role="alert">
          {language.settings.unreadable}
        </p>
      );
  }
}

import { Injectable, inject, signal } from '@angular/core';
import { FilterPreset, FilterPresetsService } from './filter-presets.service';

export interface FilterStateConfig<T> {
  /// Bucket name for localStorage presets ("transactions", "subscriptions", ...).
  storageKey: string;
  /// Default filter values — used as the "no active filter" baseline for clearAll/clearOne/hasActive.
  defaults: T;
  /// Returns the component's current filter state assembled from its individual signals.
  read: () => T;
  /// Writes filter values back into the component's signals.
  write: (values: T) => void;
  /// Called after any state mutation (clear, applyPreset, etc.) so the page can refetch/refresh URL.
  onChange?: () => void;
}

/// Per-page controller that owns the parts of filter state that every filterable list
/// duplicates: preset save/load, clearAll, clearOne, hasActive. Component still owns the
/// individual filter signals + URL ↔ filter mapping (those vary too much to generalize).
///
/// Usage:
///   private filters = new FilterStateController({ storageKey: 'transactions', defaults: {...}, read: () => this.currentFilters(), write: v => this.applyFilters(v), onChange: () => this.fetch() });
@Injectable()
export class FilterStateController<T extends Record<string, unknown>> {
  private presetsService = inject(FilterPresetsService);
  readonly presets = signal<FilterPreset<T>[]>([]);
  private config!: FilterStateConfig<T>;

  configure(config: FilterStateConfig<T>) {
    this.config = config;
    this.presets.set(this.presetsService.load<T>(config.storageKey));
  }

  hasActive(): boolean {
    const current = this.config.read();
    const defaults = this.config.defaults;
    return (Object.keys(defaults) as (keyof T)[]).some(key => !valuesEqual(current[key], defaults[key]));
  }

  clearAll() {
    this.config.write({ ...this.config.defaults });
    this.config.onChange?.();
  }

  clearOne(key: keyof T) {
    const current = this.config.read();
    this.config.write({ ...current, [key]: this.config.defaults[key] });
    this.config.onChange?.();
  }

  savePreset(name: string) {
    const trimmed = name.trim();
    if (!trimmed) return;
    this.presets.set(this.presetsService.save(this.config.storageKey, {
      name: trimmed,
      values: this.config.read(),
    }));
  }

  applyPreset(preset?: FilterPreset<T>) {
    if (!preset) return;
    this.config.write(preset.values);
    this.config.onChange?.();
  }
}

function valuesEqual(a: unknown, b: unknown): boolean {
  return a === b || (a == null && b == null);
}

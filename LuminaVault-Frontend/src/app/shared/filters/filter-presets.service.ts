import { Injectable } from '@angular/core';

export interface FilterPreset<T> {
  name: string;
  values: T;
}

@Injectable({ providedIn: 'root' })
export class FilterPresetsService {
  load<T>(key: string): FilterPreset<T>[] {
    try {
      return JSON.parse(localStorage.getItem(this.storageKey(key)) ?? '[]') as FilterPreset<T>[];
    } catch {
      return [];
    }
  }

  save<T>(key: string, preset: FilterPreset<T>) {
    const presets = this.load<T>(key).filter(p => p.name !== preset.name);
    presets.push(preset);
    localStorage.setItem(this.storageKey(key), JSON.stringify(presets));
    return presets;
  }

  private storageKey(key: string) {
    return `lv_filter_presets_${key}`;
  }
}

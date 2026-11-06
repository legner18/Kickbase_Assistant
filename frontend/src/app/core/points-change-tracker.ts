import { DestroyRef, Signal, inject, signal } from '@angular/core';

const HIGHLIGHT_DURATION_MS = 3_000;

export class PointsChangeTracker {
  private previousPoints = new Map<string, number>();
  private clearTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly currentDeltas = signal<ReadonlyMap<string, number>>(new Map());

  readonly deltas: Signal<ReadonlyMap<string, number>> = this.currentDeltas.asReadonly();

  update(entries: Iterable<readonly [string, number | null]>, highlight: boolean): void {
    const currentPoints = new Map<string, number>();
    const changes = new Map<string, number>();
    for (const [key, points] of entries) {
      if (points === null) {
        continue;
      }
      currentPoints.set(key, points);
      const previous = this.previousPoints.get(key);
      if (previous !== undefined && previous !== points) {
        changes.set(key, points - previous);
      }
    }
    this.previousPoints = currentPoints;

    if (!highlight) {
      this.clear();
      return;
    }

    if (changes.size > 0) {
      this.currentDeltas.set(changes);
      this.stopTimer();
      this.clearTimer = setTimeout(() => this.clear(), HIGHLIGHT_DURATION_MS);
    }
  }

  dispose(): void {
    this.stopTimer();
  }

  private clear(): void {
    this.stopTimer();
    if (this.currentDeltas().size > 0) {
      this.currentDeltas.set(new Map());
    }
  }

  private stopTimer(): void {
    if (this.clearTimer !== null) {
      clearTimeout(this.clearTimer);
      this.clearTimer = null;
    }
  }
}

export function createPointsChangeTracker(): PointsChangeTracker {
  const tracker = new PointsChangeTracker();
  inject(DestroyRef).onDestroy(() => tracker.dispose());
  return tracker;
}

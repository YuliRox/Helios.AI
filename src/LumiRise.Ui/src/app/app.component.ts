import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { finalize, forkJoin } from 'rxjs';

import {
  AlarmResponse,
  AlarmUpsertRequest,
  RampResponse,
  WEEK_DAYS,
  WeekDayName
} from './models/api.models';
import { AlarmApiService } from './services/alarm-api.service';

const SLOT_MINUTES = 15;
const SLOTS_PER_DAY = 24 * (60 / SLOT_MINUTES);
const DAYS_PER_WEEK = 7;
const TOTAL_WEEK_SLOTS = SLOTS_PER_DAY * DAYS_PER_WEEK;
const DEFAULT_DURATION_SLOTS = 2;
const ALARM_COLOR_CLASSES = [
  'alarm-color-0',
  'alarm-color-1',
  'alarm-color-2',
  'alarm-color-3',
  'alarm-color-4',
  'alarm-color-5',
  'alarm-color-6'
] as const;

type ResizeSide = 'left' | 'right';
type DragMode = 'move' | 'resize-left' | 'resize-right';

interface AlarmSchedule {
  id: string;
  name: string;
  enabled: boolean;
  rampMode: string;
  days: number[];
  startSlot: number;
  durationSlots: number;
}

interface AlarmSegmentView {
  key: string;
  alarmId: string;
  colorClass: string;
  name: string;
  enabled: boolean;
  topPx: number;
  heightPx: number;
  leftPct: number;
  leftCss: string;
  widthCss: string;
  isStart: boolean;
  showResize: boolean;
  timeLabel: string;
  durationLabel: string;
}

interface AlarmActionMenu {
  alarmId: string;
  x: number;
  y: number;
}

interface EditAlarmDraft {
  id: string;
  name: string;
  enabled: boolean;
  rampMode: string;
  time: string;
  selectedDays: boolean[];
}

interface DragState {
  pointerId: number;
  mode: DragMode;
  startX: number;
  startY: number;
  dayWidthPx: number;
  origin: AlarmSchedule;
  captureTarget: HTMLElement;
  hasMoved: boolean;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatDividerModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private readonly alarmApi = inject(AlarmApiService);

  @ViewChild('weekColumns', { static: false })
  private readonly weekColumnsRef?: ElementRef<HTMLElement>;

  @ViewChild('menuPanel', { static: false })
  private readonly menuPanelRef?: ElementRef<HTMLElement>;

  readonly weekDays = WEEK_DAYS;
  readonly slotHeightPx = 24;
  readonly timeAxisLabels = Array.from({ length: SLOTS_PER_DAY }, (_, slot) =>
    slot % 4 === 0 ? slotToTime(slot) : ''
  );

  alarms: AlarmResponse[] = [];
  ramps: RampResponse[] = [];

  isLoading = false;
  isRefreshing = false;
  errorMessage = '';
  editErrorMessage = '';

  actionMenu: AlarmActionMenu | null = null;
  editingAlarm: EditAlarmDraft | null = null;
  isCreatingAlarm = false;

  previewSchedule: AlarmSchedule | null = null;
  previewInvalid = false;

  calendarSegments: AlarmSegmentView[] = [];

  private currentSchedulesById = new Map<string, AlarmSchedule>();
  private dragState: DragState | null = null;
  private readonly dayNameToIndex = new Map<string, number>(
    this.weekDays.map((day, index) => [day.toLowerCase(), index])
  );
  private readonly rampDurationSlotsByMode = new Map<string, number>();
  private readonly alarmColorClassById = new Map<string, string>();

  get gridHeightPx(): number {
    return SLOTS_PER_DAY * this.slotHeightPx;
  }

  ngOnInit(): void {
    this.loadDashboard();
  }

  trackByDay(_index: number, day: WeekDayName): WeekDayName {
    return day;
  }

  trackBySegment(_index: number, segment: AlarmSegmentView): string {
    return segment.key;
  }

  reload(): void {
    this.loadDashboard();
  }

  onCreateAlarm(): void {
    const activeDay = currentWeekDayIndex();
    const selectedDays = this.ensureAtLeastOneSelectedDay(this.weekDays.map((_day, index) => index === activeDay));

    this.isCreatingAlarm = true;
    this.editingAlarm = {
      id: this.generateClientAlarmId(),
      name: 'New alarm',
      enabled: true,
      rampMode: this.getDefaultRampMode(),
      time: '07:00',
      selectedDays
    };

    this.actionMenu = null;
    this.editErrorMessage = '';
  }

  onSegmentPointerDown(event: PointerEvent, segment: AlarmSegmentView): void {
    if (event.button !== 0 || this.isAlarmBusy(segment.alarmId)) {
      return;
    }

    const schedule = this.currentSchedulesById.get(segment.alarmId);
    if (!schedule) {
      return;
    }

    const dayWidthPx = this.getDayWidthPx();
    if (dayWidthPx <= 0) {
      return;
    }

    this.actionMenu = null;
    this.previewSchedule = null;
    this.previewInvalid = false;
    this.editErrorMessage = '';

    const captureTarget = event.currentTarget as HTMLElement;
    captureTarget.setPointerCapture(event.pointerId);

    this.dragState = {
      pointerId: event.pointerId,
      mode: 'move',
      startX: event.clientX,
      startY: event.clientY,
      dayWidthPx,
      origin: schedule,
      captureTarget,
      hasMoved: false
    };

    event.preventDefault();
    event.stopPropagation();
  }

  onResizePointerDown(event: PointerEvent, segment: AlarmSegmentView, side: ResizeSide): void {
    if (event.button !== 0 || this.isAlarmBusy(segment.alarmId)) {
      return;
    }

    const schedule = this.currentSchedulesById.get(segment.alarmId);
    if (!schedule) {
      return;
    }

    const dayWidthPx = this.getDayWidthPx();
    if (dayWidthPx <= 0) {
      return;
    }

    this.actionMenu = null;
    this.previewSchedule = null;
    this.previewInvalid = false;
    this.editErrorMessage = '';

    const captureTarget = event.currentTarget as HTMLElement;
    captureTarget.setPointerCapture(event.pointerId);

    this.dragState = {
      pointerId: event.pointerId,
      mode: side === 'left' ? 'resize-left' : 'resize-right',
      startX: event.clientX,
      startY: event.clientY,
      dayWidthPx,
      origin: schedule,
      captureTarget,
      hasMoved: false
    };

    event.preventDefault();
    event.stopPropagation();
  }

  onEditAlarmFromMenu(): void {
    if (!this.actionMenu) {
      return;
    }

    const schedule = this.currentSchedulesById.get(this.actionMenu.alarmId);
    if (!schedule) {
      this.actionMenu = null;
      return;
    }

    this.editingAlarm = {
      id: schedule.id,
      name: schedule.name,
      enabled: schedule.enabled,
      rampMode: schedule.rampMode,
      time: slotToTime(schedule.startSlot),
      selectedDays: this.ensureAtLeastOneSelectedDay(this.weekDays.map((_day, dayIndex) => schedule.days.includes(dayIndex)))
    };

    this.isCreatingAlarm = false;
    this.editErrorMessage = '';
    this.actionMenu = null;
  }

  onDeleteAlarmFromMenu(): void {
    if (!this.actionMenu) {
      return;
    }

    const alarm = this.alarms.find((entry) => entry.id === this.actionMenu!.alarmId);
    if (!alarm) {
      this.actionMenu = null;
      return;
    }

    const label = alarm.name?.trim() || 'Unnamed alarm';
    const confirmed = window.confirm(`Delete alarm "${label}"?`);
    if (!confirmed) {
      return;
    }

    const id = alarm.id;
    this.isRefreshing = true;
    this.actionMenu = null;
    this.errorMessage = '';

    this.alarmApi
      .deleteAlarm(id)
      .pipe(
        finalize(() => {
          this.isRefreshing = false;
        })
      )
      .subscribe({
        next: () => {
          this.alarms = this.alarms.filter((entry) => entry.id !== id);
          this.recomputeSchedules();
          this.rebuildCalendarView();
        },
        error: () => {
          this.errorMessage = 'Failed to delete alarm.';
        }
      });
  }

  closeMenu(): void {
    this.actionMenu = null;
  }

  closeEditDialog(): void {
    this.editingAlarm = null;
    this.isCreatingAlarm = false;
    this.editErrorMessage = '';
  }

  saveEdit(): void {
    if (!this.editingAlarm) {
      return;
    }

    let schedule: AlarmSchedule | null = null;
    if (!this.isCreatingAlarm) {
      schedule = this.currentSchedulesById.get(this.editingAlarm.id) ?? null;
      if (!schedule) {
        this.editingAlarm = null;
        this.isCreatingAlarm = false;
        return;
      }
    }

    const days = this.editingAlarm.selectedDays
      .map((isSelected, index) => (isSelected ? index : -1))
      .filter((index) => index >= 0);

    if (days.length === 0) {
      this.editErrorMessage = 'Select at least one day.';
      return;
    }

    const parsedSlot = parseTimeToSlot(this.editingAlarm.time);
    if (parsedSlot === null) {
      this.editErrorMessage = 'Invalid time value.';
      return;
    }

    const trimmedName = this.editingAlarm.name.trim();
    if (trimmedName.length === 0) {
      this.editErrorMessage = 'Name is required.';
      return;
    }

    const rampMode = this.editingAlarm.rampMode.trim() || this.getDefaultRampMode();
    const durationSlots = this.getDurationSlotsForRampMode(rampMode);

    const candidate: AlarmSchedule = {
      ...(schedule ?? {}),
      id: this.editingAlarm.id,
      name: trimmedName,
      enabled: this.editingAlarm.enabled,
      rampMode,
      startSlot: parsedSlot,
      durationSlots,
      days
    };

    if (!this.isSchedulePlacementValid(candidate)) {
      this.editErrorMessage = 'This change would overlap another alarm.';
      return;
    }

    const onDone = () => {
      this.editingAlarm = null;
      this.isCreatingAlarm = false;
      this.editErrorMessage = '';
    };

    if (this.isCreatingAlarm) {
      this.createAlarm(candidate, onDone);
      return;
    }

    this.persistSchedule(candidate, onDone);
  }

  toggleEditDay(dayIndex: number, checked: boolean): void {
    if (!this.editingAlarm) {
      return;
    }

    if (!checked) {
      const selectedCount = this.editingAlarm.selectedDays.filter((isSelected) => isSelected).length;
      if (selectedCount <= 1 && this.editingAlarm.selectedDays[dayIndex]) {
        this.editErrorMessage = 'Select at least one day.';
        return;
      }
    }

    this.editingAlarm.selectedDays[dayIndex] = checked;
    this.editErrorMessage = '';
  }

  @HostListener('window:pointermove', ['$event'])
  onWindowPointerMove(event: PointerEvent): void {
    if (!this.dragState || event.pointerId !== this.dragState.pointerId) {
      return;
    }

    const deltaX = event.clientX - this.dragState.startX;
    const deltaY = event.clientY - this.dragState.startY;

    if (!this.dragState.hasMoved && Math.abs(deltaX) < 4 && Math.abs(deltaY) < 4) {
      return;
    }

    this.dragState.hasMoved = true;

    let candidate = this.dragState.origin;

    if (this.dragState.mode === 'move') {
      const dayDelta = Math.round(deltaX / this.dragState.dayWidthPx);
      const slotDelta = Math.round(deltaY / this.slotHeightPx);
      candidate = this.shiftSchedule(this.dragState.origin, dayDelta, slotDelta);
    } else {
      const side: ResizeSide = this.dragState.mode === 'resize-left' ? 'left' : 'right';
      const dayDelta = Math.round(deltaX / this.dragState.dayWidthPx);
      candidate = this.resizeScheduleDays(this.dragState.origin, side, dayDelta);
    }

    this.previewSchedule = candidate;
    this.previewInvalid = !this.isSchedulePlacementValid(candidate);
    this.rebuildCalendarView();

    event.preventDefault();
  }

  @HostListener('window:pointerup', ['$event'])
  onWindowPointerUp(event: PointerEvent): void {
    if (!this.dragState || event.pointerId !== this.dragState.pointerId) {
      return;
    }

    if (!this.dragState.hasMoved) {
      this.openMenu(this.dragState.origin.id, event.clientX, event.clientY);
      this.clearDragState();
      return;
    }

    const candidate = this.previewSchedule;
    const isValid = !this.previewInvalid && candidate && candidate.id === this.dragState.origin.id;
    const isChanged = candidate ? !isEquivalentSchedule(candidate, this.dragState.origin) : false;

    this.clearDragState();

    if (!candidate || !isValid || !isChanged) {
      return;
    }

    this.persistSchedule(candidate);
  }

  @HostListener('window:pointercancel', ['$event'])
  onWindowPointerCancel(event: PointerEvent): void {
    if (!this.dragState || event.pointerId !== this.dragState.pointerId) {
      return;
    }

    this.clearDragState();
  }

  @HostListener('document:pointerdown', ['$event'])
  onDocumentPointerDown(event: PointerEvent): void {
    if (!this.actionMenu) {
      return;
    }

    const target = event.target as Node | null;
    const menuElement = this.menuPanelRef?.nativeElement;
    if (menuElement && target && menuElement.contains(target)) {
      return;
    }

    this.actionMenu = null;
  }

  private loadDashboard(): void {
    this.isLoading = true;
    this.errorMessage = '';

    forkJoin({
      alarms: this.alarmApi.listAlarms(),
      ramps: this.alarmApi.listRamps()
    })
      .pipe(
        finalize(() => {
          this.isLoading = false;
        })
      )
      .subscribe({
        next: ({ alarms, ramps }) => {
          this.alarms = alarms;
          this.ramps = ramps;
          this.recomputeSchedules();
          this.rebuildCalendarView();
        },
        error: () => {
          this.errorMessage = 'Failed to load alarms and ramps from API.';
        }
      });
  }

  private persistSchedule(candidate: AlarmSchedule, onDone?: () => void): void {
    const request: AlarmUpsertRequest = {
      name: candidate.name,
      enabled: candidate.enabled,
      rampMode: candidate.rampMode,
      time: slotToTime(candidate.startSlot),
      daysOfWeek: candidate.days.map((dayIndex) => this.weekDays[dayIndex])
    };

    this.errorMessage = '';
    this.isRefreshing = true;

    this.alarmApi
      .updateAlarm(candidate.id, request)
      .pipe(
        finalize(() => {
          this.isRefreshing = false;
        })
      )
      .subscribe({
        next: (updatedAlarm) => {
          this.alarms = this.alarms.map((existing) =>
            existing.id === updatedAlarm.id ? updatedAlarm : existing
          );
          this.recomputeSchedules();
          this.rebuildCalendarView();
          if (onDone) {
            onDone();
          }
        },
        error: () => {
          this.errorMessage = 'Failed to update alarm.';
        }
      });
  }

  private createAlarm(candidate: AlarmSchedule, onDone?: () => void): void {
    const request: AlarmUpsertRequest = {
      name: candidate.name,
      enabled: candidate.enabled,
      rampMode: candidate.rampMode,
      time: slotToTime(candidate.startSlot),
      daysOfWeek: candidate.days.map((dayIndex) => this.weekDays[dayIndex])
    };

    this.errorMessage = '';
    this.isRefreshing = true;

    this.alarmApi
      .createAlarm(request)
      .pipe(
        finalize(() => {
          this.isRefreshing = false;
        })
      )
      .subscribe({
        next: (createdAlarm) => {
          this.alarms = [...this.alarms, createdAlarm];
          this.recomputeSchedules();
          this.rebuildCalendarView();
          if (onDone) {
            onDone();
          }
        },
        error: () => {
          this.errorMessage = 'Failed to create alarm.';
        }
      });
  }

  private openMenu(alarmId: string, x: number, y: number): void {
    this.actionMenu = {
      alarmId,
      x: Math.min(x + 10, window.innerWidth - 220),
      y: Math.min(y + 10, window.innerHeight - 140)
    };
  }

  private clearDragState(): void {
    if (this.dragState) {
      if (this.dragState.captureTarget.hasPointerCapture(this.dragState.pointerId)) {
        this.dragState.captureTarget.releasePointerCapture(this.dragState.pointerId);
      }
    }

    this.dragState = null;
    this.previewSchedule = null;
    this.previewInvalid = false;
    this.rebuildCalendarView();
  }

  private recomputeSchedules(): void {
    this.rampDurationSlotsByMode.clear();

    for (const ramp of this.ramps) {
      const mode = normalizeMode(ramp.mode);
      if (!mode) {
        continue;
      }

      const durationSlots = secondsToSlots(ramp.rampDurationSeconds);
      this.rampDurationSlotsByMode.set(mode, durationSlots);
    }

    const nextSchedules = new Map<string, AlarmSchedule>();

    for (const alarm of this.alarms) {
      const parsed = this.parseAlarm(alarm);
      if (!parsed) {
        continue;
      }

      nextSchedules.set(parsed.id, parsed);
    }

    this.currentSchedulesById = nextSchedules;
  }

  private rebuildCalendarView(): void {
    const effectiveSchedules = new Map<string, AlarmSchedule>(this.currentSchedulesById);

    if (this.previewSchedule) {
      effectiveSchedules.set(this.previewSchedule.id, this.previewSchedule);
    }

    const nextSegments: AlarmSegmentView[] = [];

    for (const schedule of effectiveSchedules.values()) {
      const dayRuns = buildContiguousDayRuns(schedule.days);
      const showResize = isDaysContiguous(schedule.days);

      for (let runIndex = 0; runIndex < dayRuns.length; runIndex += 1) {
        const run = dayRuns[runIndex];
        let remaining = schedule.durationSlots;
        let currentSlot = schedule.startSlot;
        let runSegmentIndex = 0;

        while (remaining > 0) {
          const availableSlotsToday = SLOTS_PER_DAY - currentSlot;
          const usedSlots = Math.min(remaining, availableSlotsToday);

          const spanChunks = splitSpanAcrossWeek(run.startDay + runSegmentIndex, run.span);
          for (let chunkIndex = 0; chunkIndex < spanChunks.length; chunkIndex += 1) {
            const chunk = spanChunks[chunkIndex];
            const leftPct = (chunk.startDay / DAYS_PER_WEEK) * 100;
            const widthPct = (chunk.span / DAYS_PER_WEEK) * 100;

            nextSegments.push({
              key: `${schedule.id}-${runIndex}-${runSegmentIndex}-${chunkIndex}`,
              alarmId: schedule.id,
              colorClass: this.getAlarmColorClass(schedule.id),
              name: schedule.name,
              enabled: schedule.enabled,
              topPx: currentSlot * this.slotHeightPx,
              heightPx: Math.max(usedSlots * this.slotHeightPx, this.slotHeightPx),
              leftPct,
              leftCss: `calc(${leftPct}% + 6px)`,
              widthCss: `calc(${widthPct}% - 12px)`,
              isStart: runSegmentIndex === 0 && chunkIndex === 0,
              showResize: showResize && runIndex === 0 && runSegmentIndex === 0 && chunkIndex === 0,
              timeLabel: slotToTime(schedule.startSlot),
              durationLabel: slotsToDurationLabel(schedule.durationSlots)
            });
          }

          remaining -= usedSlots;
          currentSlot = 0;
          runSegmentIndex += 1;
        }
      }
    }

    nextSegments.sort((left, right) => {
      if (left.topPx !== right.topPx) {
        return left.topPx - right.topPx;
      }

      const leftStart = left.leftPct;
      const rightStart = right.leftPct;
      if (leftStart !== rightStart) {
        return leftStart - rightStart;
      }

      return right.heightPx - left.heightPx;
    });

    this.calendarSegments = nextSegments;
  }

  private parseAlarm(alarm: AlarmResponse): AlarmSchedule | null {
    if (!alarm.time || !alarm.daysOfWeek || alarm.daysOfWeek.length === 0) {
      return null;
    }

    const startSlot = parseTimeToSlot(alarm.time);
    if (startSlot === null) {
      return null;
    }

    const days = alarm.daysOfWeek
      .map((day) => this.dayNameToIndex.get(day.trim().toLowerCase()) ?? -1)
      .filter((dayIndex) => dayIndex >= 0);

    const uniqueDays = uniqueSorted(days);
    if (uniqueDays.length === 0) {
      return null;
    }

    return {
      id: alarm.id,
      name: alarm.name?.trim() || 'Unnamed alarm',
      enabled: alarm.enabled,
      rampMode: alarm.rampMode?.trim() || this.getDefaultRampMode(),
      days: uniqueDays,
      startSlot,
      durationSlots: this.getDurationSlotsForRampMode(alarm.rampMode)
    };
  }

  private getDurationSlotsForRampMode(rampMode: string | null): number {
    const mode = normalizeMode(rampMode);
    if (!mode) {
      return DEFAULT_DURATION_SLOTS;
    }

    return this.rampDurationSlotsByMode.get(mode) ?? DEFAULT_DURATION_SLOTS;
  }

  private getDefaultRampMode(): string {
    const firstMode = this.ramps.find((ramp) => (ramp.mode?.trim().length ?? 0) > 0)?.mode;
    return firstMode?.trim() || 'Linear';
  }

  private ensureAtLeastOneSelectedDay(selectedDays: boolean[]): boolean[] {
    if (selectedDays.some((isSelected) => isSelected)) {
      return selectedDays;
    }

    const fallbackDays = [...selectedDays];
    fallbackDays[currentWeekDayIndex()] = true;
    return fallbackDays;
  }

  private shiftSchedule(origin: AlarmSchedule, dayDelta: number, slotDelta: number): AlarmSchedule {
    const absoluteSlot = origin.startSlot + slotDelta;
    const dayShiftByTime = floorDiv(absoluteSlot, SLOTS_PER_DAY);

    const startSlot = mod(absoluteSlot, SLOTS_PER_DAY);
    const totalDayShift = dayDelta + dayShiftByTime;

    const shiftedDays = uniqueSorted(origin.days.map((day) => mod(day + totalDayShift, DAYS_PER_WEEK)));

    return {
      ...origin,
      startSlot,
      days: shiftedDays
    };
  }

  private resizeScheduleDays(origin: AlarmSchedule, side: ResizeSide, dayDelta: number): AlarmSchedule {
    if (dayDelta === 0) {
      return origin;
    }

    const currentDays = new Set(origin.days);
    const minDay = Math.min(...origin.days);
    const maxDay = Math.max(...origin.days);

    if (side === 'right') {
      const newBoundary = clamp(maxDay + dayDelta, minDay, DAYS_PER_WEEK - 1);

      if (dayDelta > 0) {
        for (let day = maxDay + 1; day <= newBoundary; day += 1) {
          currentDays.add(day);
        }
      } else {
        for (let day = maxDay; day > newBoundary; day -= 1) {
          currentDays.delete(day);
        }
      }
    } else {
      const newBoundary = clamp(minDay + dayDelta, 0, maxDay);

      if (dayDelta < 0) {
        for (let day = minDay - 1; day >= newBoundary; day -= 1) {
          currentDays.add(day);
        }
      } else {
        for (let day = minDay; day < newBoundary; day += 1) {
          currentDays.delete(day);
        }
      }
    }

    const resizedDays = uniqueSorted(Array.from(currentDays));

    return {
      ...origin,
      days: resizedDays.length > 0 ? resizedDays : [minDay]
    };
  }

  private isSchedulePlacementValid(candidate: AlarmSchedule): boolean {
    const candidateUsage = this.buildUsage(candidate);
    if (candidateUsage.selfOverlap) {
      return false;
    }

    for (const [alarmId, schedule] of this.currentSchedulesById.entries()) {
      if (alarmId === candidate.id) {
        continue;
      }

      const otherUsage = this.buildUsage(schedule);
      if (hasIntersection(candidateUsage.slots, otherUsage.slots)) {
        return false;
      }
    }

    return true;
  }

  private buildUsage(schedule: AlarmSchedule): { slots: Set<number>; selfOverlap: boolean } {
    const slots = new Set<number>();
    let selfOverlap = false;

    for (const day of schedule.days) {
      const startAbsoluteSlot = day * SLOTS_PER_DAY + schedule.startSlot;

      for (let offset = 0; offset < schedule.durationSlots; offset += 1) {
        const slot = mod(startAbsoluteSlot + offset, TOTAL_WEEK_SLOTS);
        if (slots.has(slot)) {
          selfOverlap = true;
        }

        slots.add(slot);
      }
    }

    return { slots, selfOverlap };
  }

  private getDayWidthPx(): number {
    const weekColumns = this.weekColumnsRef?.nativeElement;
    if (!weekColumns) {
      return 0;
    }

    return weekColumns.getBoundingClientRect().width / DAYS_PER_WEEK;
  }

  isAlarmBusy(alarmId: string): boolean {
    return this.isRefreshing || this.isLoading || this.dragState?.origin.id === alarmId;
  }

  get alarmNameFromMenu(): string {
    if (!this.actionMenu) {
      return '';
    }

    const schedule = this.currentSchedulesById.get(this.actionMenu.alarmId);
    return schedule?.name || 'Alarm';
  }

  get availableRampModes(): string[] {
    const modes = this.ramps
      .map((ramp) => ramp.mode?.trim() || '')
      .filter((mode) => mode.length > 0);

    if (modes.length > 0) {
      return uniqueSortedStrings(modes);
    }

    return ['Linear'];
  }

  private generateClientAlarmId(): string {
    if (globalThis.crypto?.randomUUID) {
      return globalThis.crypto.randomUUID();
    }

    return `new-${Date.now()}-${Math.round(Math.random() * 1_000_000_000)}`;
  }

  private getAlarmColorClass(alarmId: string): string {
    const existing = this.alarmColorClassById.get(alarmId);
    if (existing) {
      return existing;
    }

    const hash = hashString(alarmId);
    const colorClass = ALARM_COLOR_CLASSES[hash % ALARM_COLOR_CLASSES.length];
    this.alarmColorClassById.set(alarmId, colorClass);
    return colorClass;
  }
}

interface DayRun {
  startDay: number;
  span: number;
}

interface DaySpanChunk {
  startDay: number;
  span: number;
}

function buildContiguousDayRuns(days: number[]): DayRun[] {
  const sortedDays = uniqueSorted(days);
  if (sortedDays.length === 0) {
    return [];
  }

  const runs: DayRun[] = [];
  let runStart = sortedDays[0];
  let previousDay = sortedDays[0];

  for (let index = 1; index < sortedDays.length; index += 1) {
    const day = sortedDays[index];
    if (day === previousDay + 1) {
      previousDay = day;
      continue;
    }

    runs.push({ startDay: runStart, span: previousDay - runStart + 1 });
    runStart = day;
    previousDay = day;
  }

  runs.push({ startDay: runStart, span: previousDay - runStart + 1 });
  return runs;
}

function splitSpanAcrossWeek(absoluteStartDay: number, span: number): DaySpanChunk[] {
  const chunks: DaySpanChunk[] = [];
  let dayCursor = absoluteStartDay;
  let remaining = span;

  while (remaining > 0) {
    const normalizedStart = mod(dayCursor, DAYS_PER_WEEK);
    const chunkSpan = Math.min(remaining, DAYS_PER_WEEK - normalizedStart);

    chunks.push({
      startDay: normalizedStart,
      span: chunkSpan
    });

    dayCursor += chunkSpan;
    remaining -= chunkSpan;
  }

  return chunks;
}

function isDaysContiguous(days: number[]): boolean {
  const sortedDays = uniqueSorted(days);
  if (sortedDays.length <= 1) {
    return true;
  }

  for (let index = 1; index < sortedDays.length; index += 1) {
    if (sortedDays[index] !== sortedDays[index - 1] + 1) {
      return false;
    }
  }

  return true;
}

function parseTimeToSlot(value: string): number | null {
  const match = /^(\d{2}):(\d{2})$/.exec(value.trim());
  if (!match) {
    return null;
  }

  const hour = Number(match[1]);
  const minute = Number(match[2]);

  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) {
    return null;
  }

  const totalMinutes = hour * 60 + minute;
  return Math.floor(totalMinutes / SLOT_MINUTES);
}

function slotToTime(slot: number): string {
  const totalMinutes = mod(slot, SLOTS_PER_DAY) * SLOT_MINUTES;
  const hour = Math.floor(totalMinutes / 60);
  const minute = totalMinutes % 60;

  return `${hour.toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`;
}

function secondsToSlots(seconds: number): number {
  const rawSlots = Math.round(seconds / (SLOT_MINUTES * 60));
  return clamp(rawSlots, 1, TOTAL_WEEK_SLOTS);
}

function slotsToDurationLabel(slots: number): string {
  const totalMinutes = slots * SLOT_MINUTES;
  if (totalMinutes < 60) {
    return `${totalMinutes}m`;
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (minutes === 0) {
    return `${hours}h`;
  }

  return `${hours}h ${minutes}m`;
}

function mod(value: number, divisor: number): number {
  return ((value % divisor) + divisor) % divisor;
}

function floorDiv(value: number, divisor: number): number {
  return Math.floor(value / divisor);
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function uniqueSorted(values: number[]): number[] {
  return Array.from(new Set(values)).sort((left, right) => left - right);
}

function uniqueSortedStrings(values: string[]): string[] {
  return Array.from(new Set(values)).sort((left, right) => left.localeCompare(right));
}

function normalizeMode(mode: string | null | undefined): string {
  return (mode ?? '').trim().toLowerCase();
}

function hasIntersection(left: Set<number>, right: Set<number>): boolean {
  const [smaller, larger] = left.size <= right.size ? [left, right] : [right, left];

  for (const value of smaller) {
    if (larger.has(value)) {
      return true;
    }
  }

  return false;
}

function isEquivalentSchedule(left: AlarmSchedule, right: AlarmSchedule): boolean {
  if (left.startSlot !== right.startSlot) {
    return false;
  }

  if (left.days.length !== right.days.length) {
    return false;
  }

  for (let index = 0; index < left.days.length; index += 1) {
    if (left.days[index] !== right.days[index]) {
      return false;
    }
  }

  return true;
}

function currentWeekDayIndex(): number {
  const jsWeekDay = new Date().getDay();
  return jsWeekDay === 0 ? 6 : jsWeekDay - 1;
}

function hashString(value: string): number {
  let hash = 0;
  for (let index = 0; index < value.length; index += 1) {
    hash = (hash * 31 + value.charCodeAt(index)) >>> 0;
  }

  return hash;
}

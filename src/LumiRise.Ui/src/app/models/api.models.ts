export type WeekDayName =
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'
  | 'Sunday';

export const WEEK_DAYS: WeekDayName[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday'
];

export interface AlarmResponse {
  id: string;
  name: string | null;
  enabled: boolean;
  daysOfWeek: string[] | null;
  time: string | null;
  rampMode: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AlarmUpsertRequest {
  name: string;
  daysOfWeek: string[];
  time: string;
  enabled: boolean;
  rampMode: string;
}

export interface RampResponse {
  id: string;
  mode: string | null;
  startBrightnessPercent: number;
  targetBrightnessPercent: number;
  rampDurationSeconds: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

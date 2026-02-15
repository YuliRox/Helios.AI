import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { API_BASE_URL } from '../api-base-url.token';
import {
  AlarmResponse,
  AlarmUpsertRequest,
  RampResponse
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AlarmApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  listAlarms() {
    return this.http.get<AlarmResponse[]>(`${this.apiBaseUrl}/api/Alarms`);
  }

  createAlarm(request: AlarmUpsertRequest) {
    return this.http.post<AlarmResponse>(`${this.apiBaseUrl}/api/Alarms`, request);
  }

  updateAlarm(id: string, request: AlarmUpsertRequest) {
    return this.http.put<AlarmResponse>(`${this.apiBaseUrl}/api/Alarms/${id}`, request);
  }

  deleteAlarm(id: string) {
    return this.http.delete<void>(`${this.apiBaseUrl}/api/Alarms/${id}`);
  }

  listRamps() {
    return this.http.get<RampResponse[]>(`${this.apiBaseUrl}/api/Ramps`);
  }
}

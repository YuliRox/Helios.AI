import { ApplicationConfig } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { API_BASE_URL } from './api-base-url.token';

const runtimeApiBaseUrl =
  (globalThis as { __LUMIRISE_API_BASE_URL__?: string }).__LUMIRISE_API_BASE_URL__ ??
  '';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(),
    provideAnimations(),
    {
      provide: API_BASE_URL,
      useValue: runtimeApiBaseUrl
    }
  ]
};

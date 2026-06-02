import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

window.addEventListener(
  'error',
  (event) => {
    if (event.message?.includes('ResizeObserver loop completed with undelivered notifications')) {
      event.stopImmediatePropagation();
    }
  },
  true
);

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));

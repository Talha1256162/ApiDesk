import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { ApiClientService } from './api-client.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const api = inject(ApiClientService);
  const token = localStorage.getItem('apiforge.accessToken');
  const isAuthEndpoint = /\/api\/auth\/(login|register|refresh|logout)$/i.test(request.url);
  const authenticatedRequest = token && !isAuthEndpoint
    ? request.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      })
    : request;

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      return api.refreshSession().pipe(
        switchMap((auth) => next(request.clone({
          setHeaders: {
            Authorization: `Bearer ${auth.accessToken}`
          }
        }))),
        catchError((refreshError) => {
          api.clearSession();
          return throwError(() => refreshError);
        })
      );
    })
  );
};

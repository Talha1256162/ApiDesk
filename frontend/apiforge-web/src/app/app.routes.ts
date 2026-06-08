import { Routes } from '@angular/router';
import { EmptyRouteComponent } from './empty-route.component';

export const routes: Routes = [
  { path: 'pricing', component: EmptyRouteComponent },
  { path: '**', component: EmptyRouteComponent }
];

import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { LeagueAnalysisComponent } from './pages/league-analysis/league-analysis.component';
import { LiveComponent } from './pages/live/live.component';
import { MarketScanComponent } from './pages/market-scan/market-scan.component';
import { SellAlertsComponent } from './pages/sell-alerts/sell-alerts.component';
import { SquadComponent } from './pages/squad/squad.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'kader', component: SquadComponent },
  { path: 'verkaufs-alarm', component: SellAlertsComponent },
  { path: 'transfermarkt', component: MarketScanComponent },
  { path: 'liga-analyse', component: LeagueAnalysisComponent },
  { path: 'live', component: LiveComponent },
  { path: '**', redirectTo: '' },
];

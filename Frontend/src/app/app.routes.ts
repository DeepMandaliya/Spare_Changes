import { Routes } from '@angular/router';
import { Logincomponents } from './components/logincomponents/logincomponents';
import { ConnectBankComponent } from './components/connectbankcomponent/connectbankcomponent';
import { Transactioncomponent } from './components/transactioncomponent/transactioncomponent';
import { AuthGuard } from './Guards/authguard';
import { Registercomponent } from './components/registercomponent/registercomponent';
import { Dashboardcomponent } from './components/dashboardcomponent/dashboardcomponent';
import { Donationoptions } from './components/donationoptions/donationoptions';

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: 'login', component: Logincomponents },
  { path: 'register', component: Registercomponent },
  { path: 'dashboard', component: Dashboardcomponent },
  { path: 'connect-bank', component: ConnectBankComponent },
  { path: 'connect-card', component: ConnectBankComponent },
  { path: 'transactions', component: Transactioncomponent },
  {path:'donationoptions',component:Donationoptions},
  { path: '**', redirectTo: '/dashboard' }
];

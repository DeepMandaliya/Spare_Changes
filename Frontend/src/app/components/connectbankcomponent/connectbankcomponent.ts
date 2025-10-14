// components/connect-bank/connect-bank.component.ts
import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Authservice } from '../../Services/authservice';
import { Plaidservice } from '../../Services/plaidservice';
import { BankAccount, CreditCard, PaymentMethodsResponse } from '../../Models/models';
import { CreateStripeTokenRequest } from '../../Models/models';

declare var Plaid: any;

@Component({
  selector: 'app-connect-bank',
  standalone: true,
  imports: [CommonModule, FormsModule,RouterLink],
  templateUrl: './connectbankcomponent.html',
  styleUrls: ['./connectbankcomponent.css']
})
export class ConnectBankComponent implements OnInit, OnDestroy {
  currentUser: any = null;
  linkToken: string | null = null;
  plaidItemId: string | null = null;
  paymentMethods: PaymentMethodsResponse = { bankAccounts: [], creditCards: [] };
  showPaymentSelection = false;
  isLoading = false;
  institutionName: string = '';
  hasExistingPaymentMethods: boolean = false;
  existingPaymentMethods: any[] = [];
  private plaidHandler: any = null;
  private isPlaidOpen = false;

  constructor(
    private authService: Authservice,
    private plaidService: Plaidservice,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    console.log('ConnectBankComponent initialized');
    this.loadExistingPaymentMethods();
  }

  ngOnDestroy(): void {
    this.cleanupPlaid();
  }

connectBank(): void {
  if (!this.currentUser || this.isLoading) {
    console.log('Already loading or no user');
    return;
  }

  this.isLoading = true;
  console.log('Starting bank connection...');

  // Create the configuration object
  const plaidConfig = {
    account_filters: {
      depository: {
        account_subtypes: ['checking', 'savings', 'business checking', 'business savings']
      },
      credit: {
        account_subtypes: ['credit card', 'business credit card']
      }
    }
  };

  this.plaidService.createLinkToken(
    this.currentUser.id.toString(),
    ['auth', 'transactions', 'liabilities'],
    plaidConfig // Pass as third parameter
  ).subscribe({
    next: (res: any) => {
      console.log('Link token received:', res);
      this.linkToken = res.link_token;
      this.isLoading = false;
      this.initializePlaidLink();
    },
    error: (err) => {
      console.error('Error creating link token:', err);
      this.isLoading = false;
      alert('Error connecting to bank. Please try again.');
    }
  });
}

  initializePlaidLink(): void {
    if (!this.linkToken) {
      console.error('No link token available');
      return;
    }

    if (this.isPlaidOpen) {
      console.log('Plaid is already open');
      return;
    }

    console.log('Initializing Plaid Link...');

    try {
      // Clean up any existing handler
      this.cleanupPlaid();

      this.plaidHandler = Plaid.create({
        token: this.linkToken,
        onSuccess: (public_token: string, metadata: any) => {
          console.log('Plaid Link Success - Public Token:', public_token);
          console.log('Plaid Metadata:', metadata);

          this.isPlaidOpen = false;
          this.institutionName = metadata.institution?.name || 'Your Bank';

          this.handlePlaidSuccess(public_token, metadata);
        },
        onExit: (err: any, metadata: any) => {
          console.log('Plaid Link Exit - Error:', err, 'Metadata:', metadata);
          this.isPlaidOpen = false;
          this.isLoading = false;

          if (err) {
            console.log('Plaid exited with error:', err);
            alert('Bank connection was cancelled or failed. Please try again.');
          } else {
            console.log('Plaid exited normally');
          }

          this.cleanupPlaid();
        },
        onEvent: (eventName: string, metadata: any) => {
          console.log('Plaid Event:', eventName, metadata);

          switch (eventName) {
            case 'HANDOFF':
              this.isLoading = true;
              break;
            case 'OPEN':
              this.isPlaidOpen = true;
              break;
            case 'CLOSE':
              this.isPlaidOpen = false;
              break;
          }
        }
      });

      console.log('Opening Plaid Link...');
      this.plaidHandler.open();

    } catch (error) {
      console.error('Error initializing Plaid:', error);
      this.isLoading = false;
      this.isPlaidOpen = false;
      alert('Error initializing bank connection. Please refresh the page and try again.');
    }
  }

  private handlePlaidSuccess(public_token: string, metadata: any): void {
    this.isLoading = true;

    console.log('Exchanging public token and auto-processing payment methods...');
    this.plaidService.exchangePublicToken(public_token, this.currentUser.id).subscribe({
        next: (res: any) => {
            console.log('All payment methods processed:', res);
            this.isLoading = false;
            
            // Show success message and redirect
            alert(`✅ Success! ${res.accountsProcessed} payment methods connected automatically. First account set as default.`);
            
            this.cleanupPlaid();
            this.router.navigate(['/dashboard']);
        },
        error: (err) => {
            console.error('Error connecting account:', err);
            this.isLoading = false;
            alert('Error connecting account. Please try again.');
            this.cleanupPlaid();
        }
    });
}

  loadPaymentMethods(): void {
    if (!this.plaidItemId) {
      console.error('No plaidItemId available');
      return;
    }

    this.isLoading = true;
    console.log('Loading payment methods for item:', this.plaidItemId);

    this.plaidService.getPaymentMethods(this.plaidItemId).subscribe({
      next: (res: PaymentMethodsResponse) => {
        console.log('Payment methods loaded successfully:', res);
        this.cdr.detectChanges();
        this.paymentMethods = res;
        this.isLoading = false;
        this.cdr.detectChanges();

        if (res.bankAccounts.length === 0 && res.creditCards.length === 0) {
          alert('No eligible payment methods found in your account. Please try a different bank or ensure you have checking/savings accounts or credit cards.');
        }
      },
      error: (err) => {
        console.error('Error loading payment methods:', err);
        this.isLoading = false;
        alert('Error loading payment methods. Please try again.');
      }
    });
  }

  syncUserInfo(): void {
    if (!this.plaidItemId) return;

    // This is optional - remove if you don't need it
    this.plaidService.syncUserInfo(this.plaidItemId).subscribe({
      next: (res: any) => {
        console.log('User info synced:', res);
      },
      error: (err) => {
        console.error('Error syncing user info (non-critical):', err);
        // Don't show error as this is optional
      }
    });
  }

   loadExistingPaymentMethods(): void {
    if (!this.currentUser) return;

    this.isLoading = true;
    this.plaidService.getUserPaymentMethods(this.currentUser.id).subscribe({
      next: (paymentMethods: any[]) => {
        this.existingPaymentMethods = paymentMethods || [];
        this.hasExistingPaymentMethods = this.existingPaymentMethods.length > 0;
        this.isLoading = false;
        this.cdr.detectChanges();
        console.log('Existing payment methods:', this.existingPaymentMethods);
      },
      error: (err) => {
        console.error('Error loading existing payment methods:', err);
        this.existingPaymentMethods = [];
        this.hasExistingPaymentMethods = false;
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

setDefaultPaymentMethod(paymentMethod: any): void {
    if (!paymentMethod.isActive) {
      alert('Please activate this payment method before setting it as default.');
      return;
    }

    if (confirm(`Set ${this.getPaymentMethodDisplay(paymentMethod)} as your default payment method for all donations?`)) {
      this.isLoading = true;
      
      this.plaidService.setDefaultPaymentMethod(this.currentUser.id, paymentMethod.id).subscribe({
        next: (response: any) => {
          console.log('Default payment method updated:', response);
          this.isLoading = false;
          
          // Reload payment methods to reflect the change
          this.loadExistingPaymentMethods();
          
          alert(`✅ ${this.getPaymentMethodDisplay(paymentMethod)} is now your default payment method!`);
        },
        error: (err) => {
          console.error('Error setting default payment method:', err);
          this.isLoading = false;
          const errorMsg = err.error?.message || err.error?.details || 'Failed to set default payment method';
          alert(`❌ Error: ${errorMsg}`);
        }
      });
    }
  }

 activatePaymentMethod(paymentMethod: any): void {
    if (confirm(`Activate ${this.getPaymentMethodDisplay(paymentMethod)} for donations?`)) {
      this.isLoading = true;
      
      this.plaidService.activatePaymentMethod(this.currentUser.id, paymentMethod.id).subscribe({
        next: (response: any) => {
          console.log('Payment method activated:', response);
          this.isLoading = false;
          
          // Reload payment methods
          this.loadExistingPaymentMethods();
          
          alert(`✅ ${this.getPaymentMethodDisplay(paymentMethod)} has been activated!`);
        },
        error: (err) => {
          console.error('Error activating payment method:', err);
          this.isLoading = false;
          
          if (err.error?.requiresVerification) {
            alert(`⚠️ Bank account requires verification. Please complete the verification process.`);
          } else {
            const errorMsg = err.error?.message || err.error?.details || 'Failed to activate payment method';
            alert(`❌ Error: ${errorMsg}`);
          }
        }
      });
    }
  }


  selectBankAccount(account: BankAccount): void {
    if (!this.confirmSelection('bank account', account.name)) return;
    if (!this.plaidItemId) {
      alert('Error: No bank connection found. Please try reconnecting.');
      return;
    }

    this.isLoading = true;

    const request: CreateStripeTokenRequest = {
      plaidItemId: this.plaidItemId,
      accountId: account.account_id,
      lastFour: account.mask,
      bankName: account.name
    };

    console.log('Creating bank token for:', request);

    this.plaidService.createStripeBankToken(request).subscribe({
      next: (res: any) => {
        console.log('Bank token created successfully:', res);
        alert(`✅ ${account.name} (****${account.mask}) has been successfully linked for ACH transfers!`);

        // Reload existing payment methods after successful connection
        this.loadExistingPaymentMethods();

        this.cleanupPlaid();
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        console.error('Error creating bank token:', err);
        const errorMsg = err.error?.details || err.error?.message || err.message;
        alert(`❌ Error linking bank account: ${errorMsg}`);
        this.isLoading = false;
      }
    });
  }

  selectCreditCard(card: CreditCard): void {
    if (!this.confirmSelection('credit card', card.name || 'Credit Card')) return;
    if (!this.plaidItemId) {
      alert('Error: No bank connection found. Please try reconnecting.');
      return;
    }

    this.isLoading = true;

    const request: CreateStripeTokenRequest = {
      plaidItemId: this.plaidItemId,
      accountId: card.account_id,
      lastFour: card.mask || '0000',
      bankName: card.name || 'Credit Card'
    };

    console.log('Creating credit card token for:', request);

    this.plaidService.createStripeCardToken(request).subscribe({
      next: (res: any) => {
        console.log('Credit card token created successfully:', res);

        if (res.isSandboxMock) {
          alert(`✅ Sandbox: Credit card (****${card.mask || '0000'}) linked successfully!\n\nIn production, this would process real payments.`);
        } else {
          alert(`✅ ${card.name || 'Credit Card'} (****${card.mask || '0000'}) has been successfully linked!`);
        }

        // Reload existing payment methods after successful connection
        this.loadExistingPaymentMethods();

        this.cleanupPlaid();
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        console.error('Error creating card token:', err);
        const errorMsg = err.error?.details || err.error?.message || err.message;
        alert(`❌ Error linking credit card: ${errorMsg}`);
        this.isLoading = false;
      }
    });
  }

  private cleanupPlaid(): void {
    console.log('Cleaning up Plaid...');
    this.isPlaidOpen = false;

    if (this.plaidHandler) {
      try {
        this.plaidHandler.exit({ force: true });
        this.plaidHandler.destroy();
        this.plaidHandler = null;
        console.log('Plaid handler cleaned up');
      } catch (error) {
        console.error('Error cleaning up Plaid handler:', error);
      }
    }
  }

  private confirmSelection(type: string, name: string): boolean {
    return confirm(`Do you want to use this ${type} for donations?\n\n${name}\n\nThis will be your default payment method for round-up donations.`);
  }

  cancelSelection(): void {
    if (confirm('Are you sure you want to cancel? You will need to reconnect your bank to set up payment methods.')) {
      this.showPaymentSelection = false;
      this.plaidItemId = null;
      this.paymentMethods = { bankAccounts: [], creditCards: [] };
      this.institutionName = '';
      this.cleanupPlaid();
    }
  }

  getPaymentMethodDisplay(paymentMethod: any): string {
    if (paymentMethod.type === 'card') {
      return `Credit Card ****${paymentMethod.lastFour}`;
    } else if (paymentMethod.type === 'us_bank_account' || paymentMethod.type === 'ach') {
      return `Bank Account ****${paymentMethod.lastFour}`;
    } else {
      return `${paymentMethod.brand} ****${paymentMethod.lastFour}`;
    }
  }

  getPaymentMethodIcon(paymentMethod: any): string {
    if (paymentMethod.type === 'card') {
      return 'fa-credit-card';
    } else if (paymentMethod.type === 'us_bank_account' || paymentMethod.type === 'ach') {
      return 'fa-university';
    } else {
      return 'fa-wallet';
    }
  }

  getAccountDisplayType(account: any): string {
    if (account.subtype === 'credit card' || account.type === 'credit') {
      return 'Credit Card - $0 balance means no outstanding charges';
    } else if (account.subtype === 'checking') {
      return 'Checking Account';
    } else if (account.subtype === 'savings') {
      return 'Savings Account';
    } else if (account.subtype === 'debit') {
      return 'Debit Card';
    }
    return account.subtype || account.type;
  }

 removePaymentMethod(paymentMethod: any): void {
    if (paymentMethod.isDefault) {
      alert('Cannot remove the default payment method. Please set another payment method as default first.');
      return;
    }

    if (confirm(`Are you sure you want to remove ${this.getPaymentMethodDisplay(paymentMethod)}?`)) {
      this.isLoading = true;
      
      this.plaidService.removePaymentMethod(paymentMethod.id).subscribe({
        next: () => {
          this.isLoading = false;
          this.loadExistingPaymentMethods();
          alert('Payment method removed successfully');
        },
        error: (err: any) => {
          console.error('Error removing payment method:', err);
          this.isLoading = false;
          alert('Error removing payment method. Please try again.');
        }
      });
    }
  }

  getAccountBalanceText(account: any): string {
    if (account.subtype === 'credit card' || account.type === 'credit') {
      if (account.balances.current === 0) {
        return '✅ No outstanding balance';
      } else {
        return `$${account.balances.current} current balance`;
      }
    } else {
      return `$${account.balances.current} available`;
    }
  }

  getAccountIcon(account: any): string {
    if (account.subtype === 'credit card' || account.type === 'credit') {
      return 'fa-credit-card';
    } else if (account.subtype === 'checking') {
      return 'fa-university';
    } else if (account.subtype === 'savings') {
      return 'fa-piggy-bank';
    } else if (account.subtype === 'debit') {
      return 'fa-credit-card';
    }
    return 'fa-wallet';
  }

  getAccountColor(account: any): string {
    if (account.subtype === 'credit card' || account.type === 'credit') {
      return 'success';
    } else if (account.subtype === 'checking') {
      return 'primary';
    } else if (account.subtype === 'savings') {
      return 'info';
    }
    return 'secondary';
  }
}
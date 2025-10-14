import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Authservice } from '../../Services/authservice';
import { Donationservice } from '../../Services/donationservice';
import { Plaidservice } from '../../Services/plaidservice';

@Component({
  selector: 'app-donationoptions',
  imports: [CommonModule, FormsModule],
  templateUrl: './donationoptions.html',
  styleUrl: './donationoptions.css'
})
export class Donationoptions implements OnInit {
  currentUser: any;
  charities: any[] = [];
  activeTab: string = 'direct';
  
  // Direct donation
  selectedCharityId: number = 0;
  directDonationAmount: number = 0;

  // Round-up donation
  roundUpData: any = null;
  loadingRoundUp: boolean = false;

  processing: boolean = false;
  errorMessage: string = '';
  successMessage: string = '';

  // Store user's payment methods
  userPaymentMethods: any[] = [];
  selectedPaymentMethodId: number = 0;

  // Validation properties - removed setTimeout for immediate feedback
  validationError: string = '';

  constructor(
    private authService: Authservice,
    private donationService: Donationservice,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    this.loadCharities();
    this.loadUserPaymentMethods();
  }

  loadCharities(): void {
    this.donationService.getCharities().subscribe({
      next: (charities) => {
        this.charities = charities;
        if (charities.length > 0) {
          this.selectedCharityId = charities[0].id;
        }
      },
      error: (err) => {
        console.error('Error loading charities:', err);
      }
    });
  }

  loadUserPaymentMethods(): void {
    if (this.currentUser?.id) {
      this.donationService.getUserPaymentMethods(this.currentUser.id).subscribe({
        next: (methods: any[]) => {
          this.userPaymentMethods = methods;
          console.log('✅ Loaded payment methods:', methods);
          
          if (methods && methods.length > 0) {
            const defaultMethod = methods.find(m => m.isDefault) || methods[0];
            this.selectedPaymentMethodId = defaultMethod.id;
            console.log('✅ Default payment method set to:', this.selectedPaymentMethodId);
          } else {
            this.validationError = 'No payment methods found. Please add a payment method first.';
          }
        },
        error: (err) => {
          console.error('❌ Failed to load payment methods:', err);
          this.useFallbackPaymentMethod();
        }
      });
    }
  }

  private useFallbackPaymentMethod(): void {
    console.log('🔄 Using fallback payment method logic');
    
    if (this.roundUpData && this.roundUpData.paymentMethodId) {
      this.selectedPaymentMethodId = this.roundUpData.paymentMethodId;
      console.log('✅ Using payment method from roundup data:', this.selectedPaymentMethodId);
    } 
    else if (this.currentUser.paymentmethodId) {
      this.selectedPaymentMethodId = this.currentUser.paymentmethodId;
      console.log('✅ Using payment method from user object:', this.selectedPaymentMethodId);
    }
    else {
      // this.selectedPaymentMethodId = 30;
      console.log('⚠️ Using hardcoded payment method ID for testing:', this.selectedPaymentMethodId);
      this.validationError = 'Using test payment method. Please set up a real payment method for production.';
    }
  }

  setActiveTab(tab: string): void {
    this.activeTab = tab;
    this.clearMessages();
  }

  makeDirectDonation(): void {
    // Clear previous messages
    this.clearMessages();

    // Validate and show immediate errors
    if (!this.validateAndShowError()) return;

    this.processing = true;

    const donationRequest = {
      userId: this.currentUser.id,
      charityId: this.selectedCharityId,
      amount: this.directDonationAmount,
      paymentMethodId: this.selectedPaymentMethodId
    };

    console.log('🚀 Sending direct donation request:', donationRequest);

    this.donationService.makeDirectDonation(donationRequest).subscribe({
      next: (response: any) => {
        this.processing = false;
        this.cdr.detectChanges();

        if (response.success) {
        this.cdr.detectChanges();
          if (response.status === 'completed') {
            this.successMessage = `Thank you! Your donation of $${this.directDonationAmount} to ${response.charityName} was successful.`;
        this.cdr.detectChanges();
          } else if (response.status === 'processing') {
            if (response.sandboxMode) {
              this.successMessage = `Bank donation of $${this.directDonationAmount} to ${response.charityName} is processing (Sandbox Mode). This may take a few days to complete.`;
            } else {
              this.successMessage = `Bank donation of $${this.directDonationAmount} to ${response.charityName} is processing. This may take a few days to complete.`;
            }
          }
          
          this.directDonationAmount = 0;
          this.validationError = ''; // Clear validation error on success
          
        } else {
          if (response.requiresAction) {
            this.handlePaymentAction(response.clientSecret, response.paymentIntentId);
          } else {
            this.errorMessage = response.message || 'Donation failed. Please try again.';
          }
        }
      },
      error: (err) => {
        this.processing = false;
        this.cdr.detectChanges();
        
        console.error('❌ Direct donation error:', err);
        
        if (err.error?.error === 'Payment method not found') {
          this.errorMessage = 'Payment method not found. Please select a valid payment method.';
        } else if (err.error?.error === 'Bank payment failed') {
          this.errorMessage = 'Bank payment failed. Please try again or use a different payment method.';
        } else if (err.error?.error === 'Card payment failed') {
          this.errorMessage = 'Card payment failed. Please check your card details and try again.';
        } else {
          this.errorMessage = err.error?.details || err.error?.message || 'Donation failed. Please try again.';
        }
      }
    });
  }

  // Validation without setting error messages during change detection
  isDirectDonationValid(): boolean {
    return this.selectedCharityId > 0 && 
           this.directDonationAmount > 0 && 
           this.selectedPaymentMethodId > 0;
  }

  // Separate method to show validation errors - IMMEDIATE feedback
  validateAndShowError(): boolean {
    // Clear previous validation error
    this.validationError = '';

    if (!this.selectedCharityId) {
      this.validationError = 'Please select a charity.';
      return false;
    }
    
    if (!this.directDonationAmount || this.directDonationAmount <= 0) {
      this.validationError = 'Please enter a valid donation amount.';
      return false;
    }
    
    if (!this.selectedPaymentMethodId) {
      this.validationError = 'Please select a payment method.';
      return false;
    }
    
    return true;
  }

  // Real-time validation for form fields
  onAmountChange(): void {
    if (this.directDonationAmount <= 0) {
      this.validationError = 'Please enter a valid donation amount.';
    } else {
      this.validationError = '';
    }
  }

  onCharityChange(): void {
    if (!this.selectedCharityId) {
      this.validationError = 'Please select a charity.';
    } else {
      this.validationError = '';
    }
  }

  onPaymentMethodChange(): void {
    if (!this.selectedPaymentMethodId) {
      this.validationError = 'Please select a payment method.';
    } else {
      this.validationError = '';
    }
  }

  async handlePaymentAction(clientSecret: string, paymentIntentId: string): Promise<void> {
    this.errorMessage = 'Additional payment verification required. Please check your payment method for verification steps.';
    console.log('Payment requires action:', { clientSecret, paymentIntentId });
  }

  loadRoundUpCalculation(): void {
    this.loadingRoundUp = true;
    this.clearMessages();

    this.donationService.calculateRoundUp(this.currentUser.id).subscribe({
      next: (data: any) => {
        this.loadingRoundUp = false;
        this.cdr.detectChanges();
        
        this.roundUpData = data;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loadingRoundUp = false;
        this.errorMessage = 'Failed to calculate round-up. Please try again.';
      }
    });
  }

  donateRoundUp(): void {
    if (!this.roundUpData || !this.roundUpData.summary?.totalRoundUp) {
      this.errorMessage = 'No round-up amount available. Please calculate round-up first.';
      return;
    }

    this.processing = true;
    this.clearMessages();

    const roundupRequest = {
      userId: this.currentUser.id,
      charityId: this.selectedCharityId,
      amount: this.roundUpData.summary.totalRoundUp,
      paymentMethodId: this.currentUser.paymentmethodId
    };

    this.donationService.donateRoundUp(roundupRequest).subscribe({
      next: (response: any) => {
        this.processing = false;
        this.cdr.detectChanges();

        if (response.success) {
          this.successMessage = response.message || `Awesome! You donated $${this.roundUpData.summary.totalRoundUp} from rounding up your transactions.`;
        this.cdr.detectChanges();
          this.roundUpData = null;
          
        } else {
          this.errorMessage = response.message || 'Round-up donation failed.';
        }
      },
      error: (err) => {
        this.processing = false;
        this.errorMessage = err.error?.details || err.error?.message || 'Round-up donation failed.';
      }
    });
  }

  clearMessages(): void {
    this.errorMessage = '';
    this.successMessage = '';
    this.validationError = '';
  }
}
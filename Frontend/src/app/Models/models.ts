// Models/models.ts
export interface User {
  id: number;
  username: string;
  email: string;
  stripeCustomerId: string;
  createdAt: string;
  lastLogin?: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  password: string;
  termsAccepted: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  user: User;
  message: string;
}

export interface PaymentMethod {
  id: number;
  userId: number;
  type: 'ach' | 'card';
  stripePaymentMethodId: string;
  stripeCustomerId: string;
  lastFour: string;
  brand: string;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface CreateStripeTokenRequest {
  plaidItemId: string;
  accountId: string;
  lastFour: string;
  bankName: string;
}

export interface BankAccount {
  account_id: string;
  name: string;
  mask: string;
  subtype: string;
  type: string;
  balances: {
    current: number;
  };
}

export interface CreditCard {
  account_id: string;
  name: string;
  mask: string;
  subtype: string;
  type: string;
  balances: {
    current: number;
  };
}

export interface PaymentMethodsResponse {
  bankAccounts: BankAccount[];
  creditCards: CreditCard[];
}

export interface Charity {
  id: number;
  name: string;
  description: string;
  logoUrl: string;
  website: string;
  isActive: boolean;
  createdAt: string;
}

export interface Transaction {
  id: number;
  userId: number;
  charityId?: number;
  plaidTransactionId?: string;
  stripePaymentIntentId?: string;
  stripePayoutId?: string;
  originalAmount: number;
  roundUpAmount: number;
  totalAmount: number;
  status: string;
  description: string;
  transactionDate: string;
  createdAt: string;
  processedAt?: string;
  charity?: Charity;
}

export interface UserStats {
  totalDonated: number;
  monthlyDonations: number;
  transactionCount: number;
  favoriteCharity: string;
  impact: {
    mealsProvided: number;
    treesPlanted: number;
    educationHours: number;
  };
}

export interface UserPreference {
  id: number;
  userId: number;
  defaultCharityId: number;
  autoRoundUp: boolean;
  roundUpThreshold: number;
  monthlyDonationLimit: number;
  notifyOnDonation: boolean;
  createdAt: string;
  updatedAt: string;
  defaultCharity?: Charity;
}

export interface ProviderTransaction {
  id: string;
  merchant: string;
  description: string;
  amount: number;
  date: string;
  category: string;
  roundUpAmount?: number;
}

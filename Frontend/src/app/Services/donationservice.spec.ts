import { TestBed } from '@angular/core/testing';

import { Donationservice } from './donationservice';

describe('Donationservice', () => {
  let service: Donationservice;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Donationservice);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});

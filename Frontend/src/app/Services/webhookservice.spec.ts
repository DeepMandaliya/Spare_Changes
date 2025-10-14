import { TestBed } from '@angular/core/testing';

import { Webhookservice } from './webhookservice';

describe('Webhookservice', () => {
  let service: Webhookservice;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Webhookservice);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});

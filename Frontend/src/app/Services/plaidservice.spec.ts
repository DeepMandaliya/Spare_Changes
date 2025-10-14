import { TestBed } from '@angular/core/testing';

import { Plaidservice } from './plaidservice';

describe('Plaidservice', () => {
  let service: Plaidservice;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Plaidservice);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});

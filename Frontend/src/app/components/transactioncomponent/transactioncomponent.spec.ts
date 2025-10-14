import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Transactioncomponent } from './transactioncomponent';

describe('Transactioncomponent', () => {
  let component: Transactioncomponent;
  let fixture: ComponentFixture<Transactioncomponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Transactioncomponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Transactioncomponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

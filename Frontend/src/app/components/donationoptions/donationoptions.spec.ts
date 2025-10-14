import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Donationoptions } from './donationoptions';

describe('Donationoptions', () => {
  let component: Donationoptions;
  let fixture: ComponentFixture<Donationoptions>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Donationoptions]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Donationoptions);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

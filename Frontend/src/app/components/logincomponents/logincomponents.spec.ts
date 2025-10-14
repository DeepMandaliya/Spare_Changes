import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Logincomponents } from './logincomponents';

describe('Logincomponents', () => {
  let component: Logincomponents;
  let fixture: ComponentFixture<Logincomponents>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Logincomponents]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Logincomponents);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

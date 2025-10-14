import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConnectBankComponent } from './connectbankcomponent';



describe('Connectbankcomponent', () => {
  let component: ConnectBankComponent;
  let fixture: ComponentFixture<ConnectBankComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConnectBankComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ConnectBankComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

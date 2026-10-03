import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { AuthService, CustomerInfo, CustomerTokenClaims } from './auth.service';

/**
 * Builds a token in the same format the API issues ('base64url(payloadJson).base64url(signature)').
 * The signature is not verified by 'hasValidSession' - that is the API's job - so a dummy one is
 * enough to test which tokens the browser is willing to use.
 */
function buildToken(claims: Partial<CustomerTokenClaims>, daysFromNow: number): string {
  const payload = JSON.stringify({
    Cid: claims.Cid ?? 100000,
    Email: claims.Email ?? 'girishrajus@gmail.com',
    Exp: Math.floor(Date.now() / 1000) + daysFromNow * 24 * 60 * 60
  });

  const payloadPart = btoa(payload).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${payloadPart}.dummy-signature`;
}

const SIGNED_IN_CUSTOMER: CustomerInfo = {
  CustomerID: 100000,
  FirstName: 'Girish',
  MiddleName: '',
  LastName: 'Raju',
  EmailId: 'girishrajus@gmail.com',
  MobileNumber: '',
  MobileIsd: '',
  IsGuest: false
};

describe('AuthService session', () => {
  let service: AuthService;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });

    service = TestBed.inject(AuthService);
  });

  afterEach(() => localStorage.clear());

  it('should treat a customer with a valid token as a usable session', () => {
    service.setSession(SIGNED_IN_CUSTOMER, buildToken({}, 7));

    expect(service.hasValidSession()).toBeTrue();
  });

  it('should reject a stored customer whose token is missing (session from before tokens existed)', () => {
    // Exactly how a browser that signed in before the API required a token looks: the customer is
    // still stored, but there is no token, so every order call would answer 401.
    service.setCurrentCustomer(SIGNED_IN_CUSTOMER);

    expect(service.getToken()).toBeNull();
    expect(service.getCurrentCustomer()).not.toBeNull();
    expect(service.hasValidSession()).toBeFalse();
  });

  it('should reject an expired token', () => {
    service.setSession(SIGNED_IN_CUSTOMER, buildToken({}, -1));

    expect(service.hasValidSession()).toBeFalse();
  });

  it('should reject a token that belongs to another customer', () => {
    service.setSession(SIGNED_IN_CUSTOMER, buildToken({ Cid: 100001 }, 7));

    expect(service.hasValidSession()).toBeFalse();
  });

  it('should reject a token whose payload cannot be read', () => {
    service.setSession(SIGNED_IN_CUSTOMER, 'not-a-token');

    expect(service.hasValidSession()).toBeFalse();
  });

  it('should report no usable session after signing out', () => {
    service.setSession(SIGNED_IN_CUSTOMER, buildToken({}, 7));
    expect(service.hasValidSession()).toBeTrue();

    service.clearSession();

    expect(service.hasValidSession()).toBeFalse();
    expect(service.getToken()).toBeNull();
    expect(service.getCurrentCustomer()).toBeNull();
  });
});

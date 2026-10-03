import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ApiConfigService } from './api.service';
import { PincodeLookupResult, PincodeService } from './pincode.service';

/**
 * The browser only ever asks the shop's own API for a PIN code - never the public directory directly -
 * so these tests pin down the address it calls and that the answer is handed back untouched.
 */
describe('PincodeService', () => {
  let service: PincodeService;
  let http: HttpTestingController;
  let baseServUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });

    service = TestBed.inject(PincodeService);
    http = TestBed.inject(HttpTestingController);
    baseServUrl = TestBed.inject(ApiConfigService).getBaseServUrl();
  });

  afterEach(() => http.verify());

  it('should ask the API for the PIN code', () => {
    // Two of the seven places 600001 covers: the office that delivers and one that does not. The service
    // must hand both back untouched, in the order the API sent them.
    const answered: PincodeLookupResult = {
      Result: 1,
      Messages: ['PIN code 600001 covers 7 post offices.'],
      Pincode: '600001',
      Areas: [
        { Area: 'Chennai', City: 'Chennai', State: 'Tamil Nadu', BranchType: 'Head Post Office', Delivers: true },
        { Area: 'Sowcarpet', City: 'Chennai', State: 'Tamil Nadu', BranchType: 'Sub Post Office', Delivers: false }
      ]
    };

    // Collected in an array rather than a single variable: the subscribe callback runs after the
    // assertions are written, which is exactly what the compiler cannot know.
    const results: PincodeLookupResult[] = [];
    service.lookupPincode('600001').subscribe((lookup) => results.push(lookup));

    const request = http.expectOne(baseServUrl + 'Customer/GetPincode/600001');
    expect(request.request.method).toBe('GET');
    request.flush(answered);

    expect(results.length).toBe(1);
    expect(results[0]).toEqual(answered);
  });

  it('should hand a failed lookup back as it came, so the form can say what happened', () => {
    const results: PincodeLookupResult[] = [];
    service.lookupPincode('600001').subscribe((lookup) => results.push(lookup));

    http.expectOne(baseServUrl + 'Customer/GetPincode/600001').flush({
      Result: 0,
      Messages: ['We could not find that PIN code.'],
      Pincode: '600001',
      Areas: []
    });

    expect(results.length).toBe(1);
    expect(results[0].Result).toBe(0);
    expect(results[0].Messages[0]).toContain('could not find');
  });
});

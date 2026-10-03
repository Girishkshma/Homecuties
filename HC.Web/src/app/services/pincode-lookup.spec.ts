import {
  CITY_STATE_FROM_PINCODE_HINT,
  PINCODE_LENGTH,
  PINCODE_NOT_FOUND_MESSAGE,
  PINCODE_UNAVAILABLE_MESSAGE,
  SAVED_CITY_STATE_KEPT_MESSAGE,
  isCompletePincode,
  landmarkWithArea,
  normalizePincode,
  pincodeAreaOptionText,
  pincodeAreas,
  pincodeCityStateOutcome,
  pincodePlaceLabel
} from './pincode-lookup';
import { PincodeArea, PincodeLookupResult } from './pincode.service';

/** A lookup answer with the fields each test cares about filled in, as the API would send them. */
function lookupResult(fields: Partial<PincodeLookupResult>): PincodeLookupResult {
  return {
    Result: 1,
    Messages: [],
    Pincode: '600001',
    Areas: [],
    ...fields
  };
}

/** One of the places a PIN code covers - by default a non-delivering locality of 600001. */
function pincodeArea(fields: Partial<PincodeArea>): PincodeArea {
  return {
    Area: 'Sowcarpet',
    City: 'Chennai',
    State: 'Tamil Nadu',
    BranchType: 'Sub Post Office',
    Delivers: false,
    ...fields
  };
}

/**
 * What the address forms do with a PIN code, kept out of the components so checkout and 'My Profile'
 * cannot drift apart. These tests pin down the rules that matter: only a whole PIN code is worth
 * looking up, nothing a customer typed is ever thrown away by a lookup, and a choice in the area list
 * names the place it is and says whether mail is delivered from there.
 */
describe('pincodeLookup', () => {
  describe('the PIN code itself', () => {
    it('should keep only the six digits a PIN code can be', () => {
      expect(normalizePincode('600 001')).toBe('600001');
      expect(normalizePincode('600001-2')).toBe('600001');
      expect(normalizePincode('6O0001')).toBe('60001');
      expect(PINCODE_LENGTH).toBe(6);
    });

    it('should treat an empty field as no PIN code at all', () => {
      expect(normalizePincode('')).toBe('');
      expect(isCompletePincode('')).toBeFalse();
    });

    it('should only look a PIN code up once it is whole', () => {
      expect(isCompletePincode('60000')).toBeFalse();
      expect(isCompletePincode('600001')).toBeTrue();
      expect(isCompletePincode('600 001')).toBeTrue();
    });
  });

  describe('the areas a lookup answers with', () => {
    it('should pass the areas of a successful lookup on', () => {
      const areas: PincodeArea[] = [pincodeArea({})];

      expect(pincodeAreas(lookupResult({ Areas: areas }))).toEqual(areas);
    });

    it('should treat a failed lookup as no areas at all', () => {
      const failed = lookupResult({
        Result: 0,
        Messages: ['We could not find that PIN code.'],
        Areas: [pincodeArea({})]
      });

      expect(pincodeAreas(failed)).toEqual([]);
    });

    it('should cope with an answer that carries no areas', () => {
      expect(pincodeAreas(null)).toEqual([]);
      expect(pincodeAreas(undefined)).toEqual([]);
      expect(pincodeAreas(lookupResult({ Areas: undefined as unknown as PincodeArea[] }))).toEqual([]);
    });
  });

  describe('the place a PIN code resolved to', () => {
    it('should name the city and the state', () => {
      expect(pincodePlaceLabel(pincodeArea({}))).toBe('Chennai, Tamil Nadu');
    });

    it('should leave out what the directory does not know', () => {
      expect(pincodePlaceLabel(pincodeArea({ City: '' }))).toBe('Tamil Nadu');
      expect(pincodePlaceLabel(null)).toBe('');
    });
  });

  describe('what the form says about the city and the state', () => {
    it('should say the place the PIN code filled in, and the PIN code it came from', () => {
      // The city and the state are read-only - the PIN code is the only thing that fills them - so the
      // form says where the answer came from rather than inviting a correction.
      expect(pincodeCityStateOutcome(pincodeArea({}), '600001'))
        .toEqual({ filled: true, message: 'Filled in Chennai, Tamil Nadu from PIN code 600001.' });
    });

    it('should name the PIN code the same way however it was typed', () => {
      expect(pincodeCityStateOutcome(pincodeArea({}), '600 001').message)
        .toBe('Filled in Chennai, Tamil Nadu from PIN code 600001.');
    });

    it('should send the customer back to the PIN code when half the place is all there is', () => {
      // A city without a state (or the other way round) leaves a field that cannot be typed into empty,
      // so it is not an answer: the PIN code has to be looked at again.
      expect(pincodeCityStateOutcome(pincodeArea({ State: '' }), '600001')).toEqual({
        filled: false,
        message: 'We could not fill in the city and the state from PIN code 600001. Please check the PIN code.'
      });
      expect(pincodeCityStateOutcome(pincodeArea({ City: '' }), '600001').filled).toBeFalse();
      expect(pincodeCityStateOutcome(null, '600001').filled).toBeFalse();
    });

    it('should talk about the PIN code field itself when there is no PIN code to name', () => {
      expect(pincodeCityStateOutcome(null, '').message)
        .toBe('We could not fill in the city and the state from your PIN code. Please check the PIN code.');
    });

    it('should never ask the customer to type a city or a state, which they cannot', () => {
      // Every message about a lookup that could not fill the two fields in ends at the PIN code, because
      // that is the only part of the address a customer can still change.
      const messages = [
        pincodeCityStateOutcome(null, '').message,
        PINCODE_NOT_FOUND_MESSAGE,
        PINCODE_UNAVAILABLE_MESSAGE,
        SAVED_CITY_STATE_KEPT_MESSAGE
      ];

      for (const message of messages) {
        expect(message).not.toContain('yourself');
        expect(message).not.toContain('type');
        expect(message).toContain('PIN code');
      }

      expect(CITY_STATE_FROM_PINCODE_HINT).toContain('PIN code');
    });
  });

  describe('one choice in the area list', () => {
    it('should name the place, then what kind of post office it is', () => {
      expect(pincodeAreaOptionText(pincodeArea({}))).toBe('Sowcarpet - Chennai, Tamil Nadu (Sub Post Office)');
    });

    it('should say which office the mail is delivered from', () => {
      // The office 600001 delivers from is its GPO, while the localities a customer would name in their
      // address are listed without delivery - so which one delivers is the useful thing to say.
      const gpo = pincodeArea({ Area: 'Chennai', BranchType: 'Head Post Office', Delivers: true });

      expect(pincodeAreaOptionText(gpo)).toBe('Chennai - Chennai, Tamil Nadu (Head Post Office, delivers here)');
      expect(pincodeAreaOptionText(pincodeArea({ Area: 'Flower Bazaar' })))
        .toBe('Flower Bazaar - Chennai, Tamil Nadu (Sub Post Office)');
    });

    it('should leave out what the directory does not know', () => {
      const bare = { City: '', State: '', BranchType: '' };

      expect(pincodeAreaOptionText(pincodeArea(bare))).toBe('Sowcarpet');
      expect(pincodeAreaOptionText(pincodeArea({ ...bare, Delivers: true }))).toBe('Sowcarpet (delivers here)');
      expect(pincodeAreaOptionText(null)).toBe('');
    });

    it('should cope with an office that has no name of its own', () => {
      expect(pincodeAreaOptionText(pincodeArea({ Area: '', BranchType: '' }))).toBe('Chennai, Tamil Nadu');
    });
  });

  describe('the landmark line an area goes into', () => {
    it('should write the area into an empty line', () => {
      expect(landmarkWithArea('', '', 'Sowcarpet')).toBe('Sowcarpet');
    });

    it('should replace the area an earlier lookup filled in', () => {
      // Retyping another PIN code must not stack areas up: "Sowcarpet" is ours, so it goes.
      expect(landmarkWithArea('Sowcarpet', 'Sowcarpet', 'Flower Bazaar')).toBe('Flower Bazaar');
    });

    it('should keep a landmark the customer typed and add the area to it', () => {
      expect(landmarkWithArea('Near the temple', '', 'Sowcarpet')).toBe('Near the temple, Sowcarpet');
    });

    it('should not name the area twice', () => {
      expect(landmarkWithArea('Sowcarpet, near the temple', '', 'Sowcarpet')).toBe('Sowcarpet, near the temple');
    });

    it('should leave the line alone when no area is known', () => {
      expect(landmarkWithArea('Near the temple', '', '')).toBe('Near the temple');
    });
  });
});

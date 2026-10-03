import { PincodeArea, PincodeLookupResult } from './pincode.service';

/**
 * The rules the PIN code field follows, kept out of the components so the checkout forms and the
 * address form in 'My Profile' cannot drift apart - and so they can be tested on their own.
 */

/** An Indian PIN code is six digits. */
export const PINCODE_LENGTH = 6;

/** Keeps only what a PIN code can be: digits, at most six of them. */
export function normalizePincode(value: string): string {
  return (value || '').replace(/\D/g, '').slice(0, PINCODE_LENGTH);
}

/** True once the customer has typed a whole PIN code - only then is it worth looking up. */
export function isCompletePincode(value: string): boolean {
  return normalizePincode(value).length === PINCODE_LENGTH;
}

/** The places a lookup answered with, or nothing at all when it found none. */
export function pincodeAreas(result: PincodeLookupResult | null | undefined): PincodeArea[] {
  if (!result || result.Result !== 1 || !result.Areas) {
    return [];
  }

  return result.Areas;
}

/** "Chennai, Tamil Nadu" - the place a PIN code resolved to, said back to the customer. */
export function pincodePlaceLabel(area: PincodeArea | null | undefined): string {
  const city = (area?.City || '').trim();
  const state = (area?.State || '').trim();

  return [city, state].filter((part) => part.length > 0).join(', ');
}

/**
 * One choice in the area list: the place, then what kind of post office it is - "Sowcarpet - Chennai,
 * Tamil Nadu (Sub Post Office)".
 *
 * Every post office a PIN code covers is offered, the way the public directory lists them, so the name a
 * customer would use for where they live is among the choices even when it is not the office that
 * delivers. Whether the office delivers is said on the option, because that is the one a parcel goes to.
 */
export function pincodeAreaOptionText(area: PincodeArea | null | undefined): string {
  const name = (area?.Area || '').trim();
  const place = pincodePlaceLabel(area);
  const label = name.length === 0 ? place : place.length === 0 ? name : `${name} - ${place}`;
  const branch = (area?.BranchType || '').trim();

  let note = branch;
  if (area?.Delivers) {
    note = branch.length > 0 ? `${branch}, delivers here` : 'delivers here';
  }

  if (label.length === 0) {
    return note;
  }

  return note.length > 0 ? `${label} (${note})` : label;
}

/**
 * The landmark line once an area has been picked.
 *
 * The customer's own words are never thrown away: the area a previous lookup filled in is replaced
 * (so retyping the PIN does not stack areas up), while a landmark they typed themselves is kept and the
 * chosen area is appended to it - which is what a delivery actually needs.
 */
export function landmarkWithArea(currentLine2: string, replacedArea: string, area: string): string {
  const line = (currentLine2 || '').trim();
  const previous = (replacedArea || '').trim();
  const chosen = (area || '').trim();

  if (chosen.length === 0) {
    return line;
  }

  if (line.length === 0 || line === previous) {
    return chosen;
  }

  if (line.toLowerCase().includes(chosen.toLowerCase())) {
    return line;
  }

  return `${line}, ${chosen}`;
}

/**
 * What every address form says about the city and the state, and why they are not typed into: the PIN
 * code is what fills them in, so the checkout forms and the 'My Profile' form say the same thing from
 * here rather than each wording it for itself.
 */

/** The line under the city and the state, shown until a PIN code has answered for them. */
export const CITY_STATE_FROM_PINCODE_HINT = 'Your city and state are filled in from your PIN code.';

/**
 * What a lookup's answer means for the city and the state, and what the form says about it.
 *
 * 'filled' is true only when the answer named both, which is the only way those two fields ever get a
 * value - they are read-only and cannot be typed into. When it did not, the message says so and points
 * at the PIN code, because that is the one thing the customer can still change about it.
 */
export function pincodeCityStateOutcome(
  area: PincodeArea | null | undefined,
  pincode: string
): { filled: boolean; message: string } {
  const city = (area?.City || '').trim();
  const state = (area?.State || '').trim();
  const source = pincodeSource(pincode);

  if (city.length > 0 && state.length > 0) {
    return { filled: true, message: `Filled in ${pincodePlaceLabel(area)} from ${source}.` };
  }

  return {
    filled: false,
    message: `We could not fill in the city and the state from ${source}. Please check the PIN code.`
  };
}

/** "PIN code 600001", or 'your PIN code' while the PIN code field itself is empty. */
function pincodeSource(pincode: string): string {
  const pin = normalizePincode(pincode);
  return pin.length > 0 ? `PIN code ${pin}` : 'your PIN code';
}

/**
 * Shown when the directory has no such PIN code. The city and the state are not typed into - the PIN
 * code is the only thing that fills them - so a PIN code the directory does not know is what has to be
 * corrected, and this says so instead of sending the customer looking for a field to type. (The API
 * sends this same wording; see PincodeService.)
 */
export const PINCODE_NOT_FOUND_MESSAGE =
  'We could not find that PIN code, so the city and the state cannot be filled in. Please check the PIN code.';

/** Shown when the directory could not be asked at all - nothing was wrong with the PIN code itself. */
export const PINCODE_UNAVAILABLE_MESSAGE =
  'We could not look that PIN code up right now, so the city and the state cannot be filled in. ' +
  'Please try again in a moment.';

/**
 * Shown while an address that is already saved is being edited and its PIN code could not be checked:
 * the city and the state that address was saved with are kept, so a directory that is down or slow
 * never stops a customer from correcting the rest of their address.
 */
export const SAVED_CITY_STATE_KEPT_MESSAGE =
  'We could not check that PIN code right now, so the city and the state are the ones this address was saved with.';

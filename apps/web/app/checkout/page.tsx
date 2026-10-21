import { permanentRedirect } from 'next/navigation';
export default function LegacyCheckout() {
  permanentRedirect('/checkout/steelflow');
}

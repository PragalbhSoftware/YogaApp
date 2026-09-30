export type VisitAddress = {
  line1: string;
  area: string;
  city: string;
  pin: string;
  landmark: string;
  homeAddress: string;
};

export type CustomerProfile = {
  id: string;
  name: string | null;
  phone: string;
  gender: string | null;
  role: string;
  visitAddress: VisitAddress | null;
};

export type VisitAddressInput = {
  line1: string;
  area: string;
  city: string;
  pin: string;
  landmark: string;
};

export type UpdateCustomerAccountInput = {
  name: string;
  gender: string;
};

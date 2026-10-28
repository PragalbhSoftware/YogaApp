import type { ComponentType } from "react";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AreaSettings } from "@/features/admin/components/area-settings";
import { BannerSettings } from "@/features/admin/components/banner-settings";
import { CategorySettings } from "@/features/admin/components/category-settings";
import { CitySettings } from "@/features/admin/components/city-settings";
import { NearestCitySettings } from "@/features/admin/components/nearest-city-settings";
import { CreditRulesSettings } from "@/features/admin/components/credit-rules-settings";
import { ErrorAlertSettings } from "@/features/admin/components/error-alert-settings";
import { FeeSettings } from "@/features/admin/components/fee-settings";
import { NotificationSettings } from "@/features/admin/components/notification-settings";
import { PackSettings } from "@/features/admin/components/pack-settings";
import { PageSettings } from "@/features/admin/components/page-settings";
import { PrivacySettings } from "@/features/admin/components/privacy-settings";
import { ReconciliationSettings } from "@/features/admin/components/reconciliation-settings";
import { SignInLimitSettings } from "@/features/admin/components/sign-in-limit-settings";
import { StateSettings } from "@/features/admin/components/state-settings";

function section(kicker: string, title: string, lead: string, Body: ComponentType) {
  return function AdminSectionPage() {
    return (
      <AdminPage kicker={kicker} title={title} lead={lead}>
        <Body />
      </AdminPage>
    );
  };
}

function AlertsBody() {
  return (
    <div className="space-y-8">
      <ErrorAlertSettings />
      <NotificationSettings />
    </div>
  );
}

export const StatesPage = section(
  "Locations",
  "States",
  "Add a state before its cities. A state stays on until every city in it is turned off.",
  StateSettings,
);
function CitiesBody() {
  return (
    <div className="space-y-8">
      <NearestCitySettings />
      <CitySettings />
    </div>
  );
}

export const CitiesPage = section(
  "Locations",
  "Cities",
  "Every city belongs to a state. Pick a state to see its cities, and to add another. Furthest city limits Use my location.",
  CitiesBody,
);
export const AreasPage = section(
  "Locations",
  "Areas",
  "Pick a state, then one of its cities, then add a neighbourhood. Hide a neighbourhood to take it out of browse. Existing bookings there carry on.",
  AreaSettings,
);
export const CategoriesPage = section(
  "Catalog",
  "Categories",
  "Customers see this name. The slug stays yoga so public browse does not break.",
  CategorySettings,
);
export const PacksPage = section(
  "Catalog",
  "Session packs",
  "Customers buy a pack once and book with credits. Changes apply to new purchases only. Packs can be turned off but never deleted, so past purchases keep their history.",
  PackSettings,
);
export const CreditRulesPage = section(
  "Catalog",
  "Credit rules",
  "Applies to new purchases only. Each purchase keeps the rules in force when it was bought.",
  CreditRulesSettings,
);
export const PolicyPagesPage = section(
  "Content",
  "Policy pages",
  "Refund policy, Terms and Privacy are linked in the site footer. Changes go live as soon as you save.",
  PageSettings,
);
export const BannerPage = section(
  "Content",
  "Home banner",
  "The headline customers see on the home page. Leave a field empty to show the default text.",
  BannerSettings,
);
export const ConsentPage = section(
  "Content",
  "Consent and data",
  "Raise the policy version after an important change to Terms or Privacy, and every user is asked once to accept again when they next sign in or their session renews.",
  PrivacySettings,
);
export const FeesPage = section(
  "Finance",
  "Fees and payouts",
  "Changes apply to bookings made after you save. Existing bookings keep the fees they were booked with.",
  FeeSettings,
);
export const ReconciliationSettingsPage = section(
  "Finance",
  "Reconciliation settings",
  "Once a day we compare Razorpay with our records for the previous day and list anything that differs under Reconciliation. Nothing is changed automatically.",
  ReconciliationSettings,
);
export const SignInPage = section(
  "Settings",
  "Sign-in limits",
  "Stops one phone number from being flooded with codes or guessed at. Codes and wrong tries are counted over the lock time; reaching either limit locks that number for the lock time.",
  SignInLimitSettings,
);
export const AlertsPage = section(
  "Settings",
  "Alerts",
  "Error emails, and emails to customers and instructors when a booking is requested, confirmed, cancelled or refunded.",
  AlertsBody,
);

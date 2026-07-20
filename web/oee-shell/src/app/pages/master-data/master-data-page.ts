import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PrimeTemplate } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { FluidModule } from 'primeng/fluid';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { PasswordModule } from 'primeng/password';
import { TableModule } from 'primeng/table';
import { SelectModule } from 'primeng/select';
import { AuthService } from '../../core/auth/auth.service';
import {
  LineDto,
  LossCategoryValue,
  MachineDto,
  MasterDataService,
  ReasonCodeDto,
  ShiftScheduleDto,
  SiteDto,
  UserDto,
  UserRoleValue,
} from './master-data.service';

const USER_ROLES: UserRoleValue[] = ['Admin', 'Manager', 'Operator', 'Viewer'];
const LOSS_CATEGORIES: LossCategoryValue[] = ['AvailabilityLoss', 'PerformanceLoss', 'QualityLoss'];

type Level = 'site' | 'line' | 'machine';

interface DialogState {
  visible: boolean;
  level: Level;
  mode: 'create' | 'edit';
  id: string | null;
}

const CLOSED_DIALOG: DialogState = { visible: false, level: 'site', mode: 'create', id: null };

interface ShiftDialogState {
  visible: boolean;
  mode: 'create' | 'edit';
  id: string | null;
}

const CLOSED_SHIFT_DIALOG: ShiftDialogState = { visible: false, mode: 'create', id: null };

interface ReasonDialogState {
  visible: boolean;
}

const CLOSED_REASON_DIALOG: ReasonDialogState = { visible: false };

interface ApiErrorBody {
  code?: string;
  message?: string;
  details?: { dependentNames?: string[] };
}

/** `<input type="time">` uses "HH:mm"; the API's TimeOnly serializes as "HH:mm:ss". */
function toApiTime(inputTime: string): string {
  return `${inputTime}:00`;
}

function toInputTime(apiTime: string): string {
  return apiTime.slice(0, 5);
}

/** Site &gt; Line &gt; Machine master-detail CRUD (Story 1.2, AC #1-#5, FR-011) plus Shift Schedules (Story 1.3, AC #1-#3, FR-012). */
@Component({
  selector: 'app-master-data-page',
  standalone: true,
  imports: [
    FormsModule,
    TranslatePipe,
    PrimeTemplate,
    ButtonModule,
    CardModule,
    DialogModule,
    FluidModule,
    InputTextModule,
    PasswordModule,
    MultiSelectModule,
    TableModule,
    SelectModule,
  ],
  templateUrl: './master-data-page.html',
  styleUrl: './master-data-page.scss',
})
export class MasterDataPage implements OnInit {
  readonly sites = signal<SiteDto[]>([]);
  readonly lines = signal<LineDto[]>([]);
  readonly machines = signal<MachineDto[]>([]);
  readonly shiftSchedules = signal<ShiftScheduleDto[]>([]);
  readonly reasonCodes = signal<ReasonCodeDto[]>([]);
  readonly selectedSite = signal<SiteDto | null>(null);
  readonly selectedLine = signal<LineDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly dialog = signal<DialogState>(CLOSED_DIALOG);
  readonly dialogName = signal('');
  readonly saving = signal(false);

  readonly shiftDialog = signal<ShiftDialogState>(CLOSED_SHIFT_DIALOG);
  readonly shiftDialogName = signal('');
  readonly shiftDialogLineId = signal<string | null>(null);
  readonly shiftDialogStartTime = signal('');
  readonly shiftDialogEndTime = signal('');
  readonly shiftSaving = signal(false);

  readonly reasonDialog = signal<ReasonDialogState>(CLOSED_REASON_DIALOG);
  readonly reasonDialogName = signal('');
  readonly reasonDialogLossCategory = signal<LossCategoryValue>('AvailabilityLoss');
  readonly reasonSaving = signal(false);

  readonly users = signal<UserDto[]>([]);
  readonly userDialogVisible = signal(false);
  readonly userDialogUsername = signal('');
  readonly userDialogPassword = signal('');
  readonly userDialogRole = signal<UserRoleValue>('Manager');
  readonly userDialogSiteIds = signal<string[]>([]);
  readonly userDialogLineIds = signal<string[]>([]);
  readonly userDialogLineOptions = signal<LineDto[]>([]);
  readonly userSaving = signal(false);

  readonly isAdmin = computed(() => this.auth.role() === 'Admin');
  readonly dialogLevelLabelKey = computed(() => `masterData.${this.dialog().level}s`);
  readonly shiftLineOptions = computed(() => [{ id: null, name: this.translate.instant('masterData.allLines') }, ...this.lines()]);
  readonly userRoleOptions = USER_ROLES.map((role) => ({ value: role, labelKey: `masterData.role.${role.toLowerCase()}` }));
  readonly lossCategoryOptions = LOSS_CATEGORIES.map((category) => ({
    value: category,
    labelKey: `masterData.lossCategory.${category}`,
  }));
  readonly showUserScopeFields = computed(() => this.userDialogRole() !== 'Admin');
  readonly showUserLineField = computed(() => this.userDialogRole() === 'Operator');

  constructor(
    private readonly masterData: MasterDataService,
    private readonly auth: AuthService,
    private readonly translate: TranslateService,
  ) {}

  private siteSelectionToken = 0;
  private lineSelectionToken = 0;

  ngOnInit(): void {
    void this.loadSites();
    if (this.isAdmin()) {
      void this.loadUsers();
    }
  }

  async loadUsers(): Promise<void> {
    try {
      this.users.set(await this.masterData.listUsers());
    } catch (err) {
      this.error.set(this.describeError(err));
    }
  }

  async loadSites(): Promise<void> {
    try {
      this.sites.set(await this.masterData.listSites());
    } catch (err) {
      this.error.set(this.describeError(err));
    }
  }

  async selectSite(site: SiteDto): Promise<void> {
    this.selectedSite.set(site);
    this.selectedLine.set(null);
    this.lines.set([]);
    this.machines.set([]);
    this.shiftSchedules.set([]);
    this.reasonCodes.set([]);
    const token = ++this.siteSelectionToken;
    try {
      const [lines, shiftSchedules, reasonCodes] = await Promise.all([
        this.masterData.listLines(site.id),
        this.masterData.listShiftSchedules(site.id),
        this.masterData.listReasonCodes(site.id),
      ]);
      if (token === this.siteSelectionToken) {
        this.lines.set(lines);
        this.shiftSchedules.set(shiftSchedules);
        this.reasonCodes.set(reasonCodes);
      }
    } catch (err) {
      if (token === this.siteSelectionToken) {
        this.error.set(this.describeError(err));
      }
    }
  }

  async selectLine(line: LineDto): Promise<void> {
    this.selectedLine.set(line);
    this.machines.set([]);
    const token = ++this.lineSelectionToken;
    try {
      const machines = await this.masterData.listMachines(line.id);
      if (token === this.lineSelectionToken) {
        this.machines.set(machines);
      }
    } catch (err) {
      if (token === this.lineSelectionToken) {
        this.error.set(this.describeError(err));
      }
    }
  }

  openCreate(level: Level): void {
    this.error.set(null);
    this.dialogName.set('');
    this.dialog.set({ visible: true, level, mode: 'create', id: null });
  }

  openEdit(level: Level, id: string, name: string): void {
    this.error.set(null);
    this.dialogName.set(name);
    this.dialog.set({ visible: true, level, mode: 'edit', id });
  }

  closeDialog(): void {
    this.dialog.set(CLOSED_DIALOG);
    this.dialogName.set('');
  }

  async save(): Promise<void> {
    const state = this.dialog();
    const name = this.dialogName().trim();
    if (!state.visible || !name) {
      return;
    }

    this.saving.set(true);
    try {
      if (state.level === 'site') {
        await this.saveSite(state, name);
      } else if (state.level === 'line') {
        await this.saveLine(state, name);
      } else {
        await this.saveMachine(state, name);
      }
      this.closeDialog();
    } catch (err) {
      await this.handleError(err);
    } finally {
      this.saving.set(false);
    }
  }

  async deleteSite(site: SiteDto): Promise<void> {
    if (!confirm(this.translate.instant('masterData.confirmDelete', { name: site.name }))) {
      return;
    }
    this.error.set(null);
    try {
      await this.masterData.deleteSite(site.id);
      this.sites.update((list) => list.filter((s) => s.id !== site.id));
      if (this.selectedSite()?.id === site.id) {
        this.siteSelectionToken++;
        this.selectedSite.set(null);
        this.selectedLine.set(null);
        this.lines.set([]);
        this.machines.set([]);
        this.shiftSchedules.set([]);
        this.reasonCodes.set([]);
      }
    } catch (err) {
      await this.handleError(err);
    }
  }

  async deleteLine(line: LineDto): Promise<void> {
    if (!confirm(this.translate.instant('masterData.confirmDelete', { name: line.name }))) {
      return;
    }
    this.error.set(null);
    try {
      await this.masterData.deleteLine(line.id);
      this.lines.update((list) => list.filter((l) => l.id !== line.id));
      if (this.selectedLine()?.id === line.id) {
        this.lineSelectionToken++;
        this.selectedLine.set(null);
        this.machines.set([]);
      }
    } catch (err) {
      await this.handleError(err);
    }
  }

  async deleteMachine(machine: MachineDto): Promise<void> {
    if (!confirm(this.translate.instant('masterData.confirmDelete', { name: machine.name }))) {
      return;
    }
    this.error.set(null);
    try {
      await this.masterData.deleteMachine(machine.id);
      this.machines.update((list) => list.filter((m) => m.id !== machine.id));
    } catch (err) {
      await this.handleError(err);
    }
  }

  private async saveSite(state: DialogState, name: string): Promise<void> {
    if (state.mode === 'create') {
      const site = await this.masterData.createSite(name);
      this.sites.update((list) => [...list, site]);
    } else {
      const site = await this.masterData.renameSite(state.id!, name);
      this.sites.update((list) => list.map((s) => (s.id === site.id ? site : s)));
    }
  }

  private async saveLine(state: DialogState, name: string): Promise<void> {
    const siteId = this.selectedSite()?.id;
    if (!siteId) {
      return;
    }
    if (state.mode === 'create') {
      const line = await this.masterData.createLine(siteId, name);
      this.lines.update((list) => [...list, line]);
    } else {
      const line = await this.masterData.renameLine(state.id!, name);
      this.lines.update((list) => list.map((l) => (l.id === line.id ? line : l)));
    }
  }

  openCreateShift(): void {
    this.error.set(null);
    this.shiftDialogName.set('');
    this.shiftDialogLineId.set(null);
    this.shiftDialogStartTime.set('');
    this.shiftDialogEndTime.set('');
    this.shiftDialog.set({ visible: true, mode: 'create', id: null });
  }

  openEditShift(shift: ShiftScheduleDto): void {
    this.error.set(null);
    this.shiftDialogName.set(shift.name);
    this.shiftDialogLineId.set(shift.lineId);
    this.shiftDialogStartTime.set(toInputTime(shift.startTime));
    this.shiftDialogEndTime.set(toInputTime(shift.endTime));
    this.shiftDialog.set({ visible: true, mode: 'edit', id: shift.id });
  }

  closeShiftDialog(): void {
    this.shiftDialog.set(CLOSED_SHIFT_DIALOG);
  }

  async saveShift(): Promise<void> {
    const state = this.shiftDialog();
    const name = this.shiftDialogName().trim();
    const startTime = this.shiftDialogStartTime();
    const endTime = this.shiftDialogEndTime();
    const siteId = this.selectedSite()?.id;
    if (!state.visible || !name || !startTime || !endTime || !siteId) {
      return;
    }

    this.shiftSaving.set(true);
    try {
      if (state.mode === 'create') {
        const shift = await this.masterData.createShiftSchedule(siteId, name, this.shiftDialogLineId(), toApiTime(startTime), toApiTime(endTime));
        this.shiftSchedules.update((list) => [...list, shift]);
      } else {
        const shift = await this.masterData.rescheduleShiftSchedule(state.id!, name, toApiTime(startTime), toApiTime(endTime));
        this.shiftSchedules.update((list) => list.map((s) => (s.id === shift.id ? shift : s)));
      }
      this.closeShiftDialog();
    } catch (err) {
      await this.handleError(err);
    } finally {
      this.shiftSaving.set(false);
    }
  }

  async deleteShift(shift: ShiftScheduleDto): Promise<void> {
    if (!confirm(this.translate.instant('masterData.confirmDeleteShift', { name: shift.name }))) {
      return;
    }
    this.error.set(null);
    try {
      await this.masterData.deleteShiftSchedule(shift.id);
      this.shiftSchedules.update((list) => list.filter((s) => s.id !== shift.id));
    } catch (err) {
      await this.handleError(err);
    }
  }

  openCreateReasonCode(): void {
    this.error.set(null);
    this.reasonDialogName.set('');
    this.reasonDialogLossCategory.set('AvailabilityLoss');
    this.reasonDialog.set({ visible: true });
  }

  closeReasonDialog(): void {
    this.reasonDialog.set(CLOSED_REASON_DIALOG);
  }

  async saveReasonCode(): Promise<void> {
    const name = this.reasonDialogName().trim();
    const siteId = this.selectedSite()?.id;
    if (!name || !siteId) {
      return;
    }

    this.reasonSaving.set(true);
    try {
      const reasonCode = await this.masterData.createReasonCode(siteId, name, this.reasonDialogLossCategory());
      this.reasonCodes.update((list) => [...list, reasonCode]);
      this.closeReasonDialog();
    } catch (err) {
      await this.handleError(err);
    } finally {
      this.reasonSaving.set(false);
    }
  }

  async deactivateReasonCode(reasonCode: ReasonCodeDto): Promise<void> {
    this.error.set(null);
    try {
      const updated = await this.masterData.deactivateReasonCode(reasonCode.id);
      this.reasonCodes.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
    } catch (err) {
      await this.handleError(err);
    }
  }

  openCreateUser(): void {
    this.error.set(null);
    this.userDialogUsername.set('');
    this.userDialogPassword.set('');
    this.userDialogRole.set('Manager');
    this.userDialogSiteIds.set([]);
    this.userDialogLineIds.set([]);
    this.userDialogLineOptions.set([]);
    this.userDialogVisible.set(true);
  }

  closeUserDialog(): void {
    this.userDialogVisible.set(false);
  }

  onUserRoleChange(role: UserRoleValue): void {
    this.userDialogRole.set(role);
    if (role === 'Admin') {
      this.userDialogSiteIds.set([]);
      this.userDialogLineIds.set([]);
      this.userDialogLineOptions.set([]);
    }
  }

  async onUserSiteIdsChange(siteIds: string[]): Promise<void> {
    this.userDialogSiteIds.set(siteIds);
    if (siteIds.length === 0) {
      this.userDialogLineOptions.set([]);
      this.userDialogLineIds.set([]);
      return;
    }
    const lineLists = await Promise.all(siteIds.map((siteId) => this.masterData.listLines(siteId)));
    const options = lineLists.flat();
    this.userDialogLineOptions.set(options);
    const validIds = new Set(options.map((l) => l.id));
    this.userDialogLineIds.update((ids) => ids.filter((id) => validIds.has(id)));
  }

  async saveUser(): Promise<void> {
    const username = this.userDialogUsername().trim();
    const password = this.userDialogPassword();
    const role = this.userDialogRole();
    if (!username || !password) {
      return;
    }

    this.userSaving.set(true);
    try {
      const user = await this.masterData.createUser(username, password, role, this.userDialogSiteIds(), this.userDialogLineIds());
      this.users.update((list) => [...list, user]);
      this.closeUserDialog();
    } catch (err) {
      await this.handleError(err);
    } finally {
      this.userSaving.set(false);
    }
  }

  lineNameFor(lineId: string | null): string {
    if (!lineId) {
      return this.translate.instant('masterData.allLines');
    }
    return this.lines().find((l) => l.id === lineId)?.name ?? lineId;
  }

  private async saveMachine(state: DialogState, name: string): Promise<void> {
    const lineId = this.selectedLine()?.id;
    if (!lineId) {
      return;
    }
    if (state.mode === 'create') {
      const machine = await this.masterData.createMachine(lineId, name);
      this.machines.update((list) => [...list, machine]);
    } else {
      const machine = await this.masterData.renameMachine(state.id!, name);
      this.machines.update((list) => list.map((m) => (m.id === machine.id ? machine : m)));
    }
  }

  private async handleError(err: unknown): Promise<void> {
    this.error.set(this.describeError(err));
    const code = err instanceof HttpErrorResponse ? (err.error as ApiErrorBody | null)?.code : undefined;
    if (code === 'NOT_FOUND' || code === 'PARENT_NOT_FOUND') {
      await this.refreshAfterNotFound();
    }
  }

  private async refreshAfterNotFound(): Promise<void> {
    await this.loadSites();

    const site = this.selectedSite();
    const line = this.selectedLine();
    if (!site) {
      return;
    }
    if (!this.sites().some((s) => s.id === site.id)) {
      this.selectedSite.set(null);
      this.selectedLine.set(null);
      this.lines.set([]);
      this.machines.set([]);
      this.shiftSchedules.set([]);
      this.reasonCodes.set([]);
      return;
    }
    await this.selectSite(site);

    if (line && this.lines().some((l) => l.id === line.id)) {
      await this.selectLine(line);
    }
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const body = err.error as ApiErrorBody | null;
      if (body?.code === 'HAS_DEPENDENTS') {
        const dependents = (body.details?.dependentNames ?? []).join(', ');
        return this.translate.instant('masterData.error.hasDependents', { dependents });
      }
      if (body?.code === 'PARENT_NOT_FOUND') {
        return this.translate.instant('masterData.error.parentNotFound');
      }
      if (body?.code === 'NOT_FOUND') {
        return this.translate.instant('masterData.error.notFound');
      }
      if (body?.code === 'SHIFT_OVERLAP') {
        return this.translate.instant('masterData.error.shiftOverlap', { message: body.message ?? '' });
      }
      if (body?.code === 'VALIDATION_ERROR') {
        return this.translate.instant('masterData.error.validationError', { message: body.message ?? '' });
      }
      if (body?.code === 'FORBIDDEN') {
        return this.translate.instant('masterData.error.forbidden');
      }
      if (body?.code === 'USERNAME_TAKEN') {
        return this.translate.instant('masterData.error.usernameTaken');
      }
    }
    return this.translate.instant('masterData.error.generic');
  }
}

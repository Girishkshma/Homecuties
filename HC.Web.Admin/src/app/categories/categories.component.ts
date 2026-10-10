import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { AdminManagedCategory, CategoryFormRequest } from '../models/admin.model';
import {
  CATEGORY_NAME_MAX_LENGTH,
  CategoryChoice,
  catalogueNote,
  categoryType,
  deleteBlockerSentence,
  parentChoices
} from './catalogue';

/**
 * The shop's own catalogue: the headings and the shelves beneath them that products are filed under.
 *
 * The screen is the one place the catalogue itself is edited - a category is added, renamed, moved from one heading to
 * another (or out to be a heading of its own) and taken out - and it is drawn from the server's own list, which says
 * where each row sits and what is filed on it (see AdminService.getManagedCategories and, on the server,
 * AdminDashboardService.GetManagedCategoriesAsync). Nothing here decides what may be written: the rules are the
 * server's (HC.Business.CategoryCatalog), and every refusal it answers with - a second shelf of the same name under one
 * heading, a heading moved under one of its own shelves, a delete that would leave shelves naming a parent that is gone
 * - is shown as the sentence it came back with rather than re-worded here.
 *
 * The one thing the screen does decide is what it offers: the 'sits under' list leaves out the row being edited and
 * everything beneath it (a category cannot sit under itself or under one of its own shelves), and the delete
 * confirmation says what is in the way before the admin asks, so the refusal is never a surprise (see ./catalogue).
 */
@Component({
  selector: 'app-categories',
  templateUrl: './categories.component.html',
  styleUrls: ['./categories.component.scss'],
  standalone: false
})
export class CategoriesComponent implements OnInit {
  categories: AdminManagedCategory[] = [];
  isLoading = true;
  loadError = '';

  /** Add/Edit form. 'editingCategoryId' is null while a category is being added. */
  showFormModal = false;
  editingCategoryId: number | null = null;
  isSaving = false;
  formMessage = '';
  formError = false;
  categoryForm: CategoryFormRequest = this.emptyForm();
  /** The categories the form may put this one under, by path, as the row being edited allows. */
  parentOptions: CategoryChoice[] = [];

  /** The category the delete confirmation is about, or null when it is not open. */
  deletingCategory: AdminManagedCategory | null = null;
  isDeleting = false;
  deleteMessage = '';
  deleteError = false;

  readonly nameMaxLength = CATEGORY_NAME_MAX_LENGTH;

  constructor(
    private adminService: AdminService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadCategories();
  }

  /**
   * Reads the catalogue. A failed read is said on the screen rather than left as an empty list: 'the catalogue could
   * not be read' and 'the catalogue is empty' are two different things, and only one of them is something the shop can
   * act on by adding a category.
   */
  loadCategories(): void {
    this.isLoading = true;
    this.loadError = '';

    this.adminService.getManagedCategories().subscribe({
      next: (categories) => {
        this.categories = categories;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.loadError = 'The catalogue could not be read. Please check your connection and try again.';
      }
    });
  }

  /** True while the list on the screen is the server's own list of the catalogue. */
  get hasCatalogue(): boolean {
    return !this.isLoading && !this.loadError;
  }

  /** The headings of the catalogue - what products are filed under, never on. */
  get headings(): AdminManagedCategory[] {
    return this.categories.filter(category => this.typeOf(category) === 'Heading');
  }

  /** Every category read under a heading, at any depth. */
  get shelves(): AdminManagedCategory[] {
    return this.categories.filter(category => this.typeOf(category) === 'Shelf');
  }

  typeOf(category: AdminManagedCategory): 'Heading' | 'Shelf' {
    return categoryType(category, this.categories);
  }

  /**
   * True for a heading - a row that names a collection and holds no product of its own. It is the same reading as
   * typeOf, said once more so the list can draw a heading apart from the shelves read beneath it.
   */
  isHeading(category: AdminManagedCategory): boolean {
    return this.typeOf(category) === 'Heading';
  }

  /** What is broken about a row (a parent that is gone, or a loop), or '' - shown under the category's name. */
  noteOf(category: AdminManagedCategory): string {
    return catalogueNote(category, this.categories);
  }

  /**
   * How far a row is stepped in, in pixels: a heading flush left, a shelf of one in under it and a shelf of a shelf in
   * under that. The step is what says 'this one is read beneath the row above', so it is spent on the shelves alone.
   */
  indentOf(category: AdminManagedCategory): number {
    return category.depth * 20;
  }

  openAddModal(): void {
    this.editingCategoryId = null;
    this.categoryForm = this.emptyForm();
    this.openForm();
  }

  /** 'Add a shelf': the form opens with this category already chosen as the one the new one sits under. */
  openAddShelfModal(category: AdminManagedCategory): void {
    this.editingCategoryId = null;
    this.categoryForm = { categoryName: '', parentCategoryId: category.categoryId };
    this.openForm();
  }

  openEditModal(categoryId: number): void {
    const category = this.categories.find(row => row.categoryId === categoryId);
    if (!category) {
      return;
    }

    this.editingCategoryId = category.categoryId;
    this.categoryForm = {
      categoryName: category.categoryName,
      parentCategoryId: category.parentCategoryId ?? null
    };
    this.openForm();
  }

  closeFormModal(): void {
    this.showFormModal = false;
    this.editingCategoryId = null;
    this.formMessage = '';
    this.formError = false;
  }

  get isEditMode(): boolean {
    return this.editingCategoryId !== null;
  }

  /** True when the form is adding a shelf: a parent is already chosen, so the new category is not a heading. */
  get isAddingShelf(): boolean {
    return !this.isEditMode && this.categoryForm.parentCategoryId != null;
  }

  saveCategory(): void {
    const name = (this.categoryForm.categoryName ?? '').trim();

    // What the form can say itself: a name has to be there and fit the column. Everything else about the name and about
    // the place it is going - a second category of the same name under one parent, a parent that is one of the
    // category's own shelves - is the server's to answer (HC.Business.CategoryCatalog), and its sentence is what the
    // form shows below.
    if (!name) {
      this.formMessage = 'Category name is required.';
      this.formError = true;
      return;
    }
    if (name.length > this.nameMaxLength) {
      this.formMessage = `Category name cannot be longer than ${this.nameMaxLength} characters.`;
      this.formError = true;
      return;
    }

    const actor = this.authService.getUser();
    const actorUserId = actor?.userId ?? 0;
    const request: CategoryFormRequest = {
      categoryName: name,
      parentCategoryId: this.categoryForm.parentCategoryId ?? null
    };

    this.isSaving = true;
    this.formMessage = '';
    this.formError = false;

    const saving = this.editingCategoryId === null
      ? this.adminService.createCategory(request, actorUserId)
      : this.adminService.updateCategory(this.editingCategoryId, request, actorUserId);

    saving.subscribe({
      next: (result) => this.handleSaveResult(result),
      error: () => this.handleSaveError()
    });
  }

  handleSaveResult(result: { result: number; messages: string[] }): void {
    this.isSaving = false;
    this.formMessage = result.messages[0];
    this.formError = result.result !== 1;

    if (result.result === 1) {
      this.loadCategories();
      setTimeout(() => this.closeFormModal(), 1200);
    }
  }

  handleSaveError(): void {
    this.isSaving = false;
    this.formMessage = 'Unable to save. Please check your connection and try again.';
    this.formError = true;
  }

  /** Opens the form with the choices the row being edited allows as its parent (see ./catalogue). */
  private openForm(): void {
    this.parentOptions = parentChoices(this.categories, this.editingCategoryId);
    this.formMessage = '';
    this.formError = false;
    this.showFormModal = true;
  }

  askToDelete(category: AdminManagedCategory): void {
    this.deletingCategory = category;
    this.deleteMessage = '';
    this.deleteError = false;
  }

  cancelDelete(): void {
    this.deletingCategory = null;
    this.deleteMessage = '';
    this.deleteError = false;
  }

  /** What is in the way of this delete, as a sentence, or '' when nothing is (see ./catalogue). */
  get deleteBlockers(): string {
    return this.deletingCategory ? deleteBlockerSentence(this.deletingCategory) : '';
  }

  deleteCategory(): void {
    if (!this.deletingCategory || this.deleteBlockers) {
      return;
    }

    const actor = this.authService.getUser();
    const actorUserId = actor?.userId ?? 0;

    this.isDeleting = true;
    this.deleteMessage = '';
    this.deleteError = false;

    this.adminService.deleteCategory(this.deletingCategory.categoryId, actorUserId).subscribe({
      next: (result) => {
        this.isDeleting = false;

        // The server is the one that refuses a delete which would leave something behind, and its reason is what is
        // shown: the counts above were read from the list on the screen, which may have moved on since.
        if (result.result === 1) {
          this.cancelDelete();
        } else {
          this.deleteMessage = result.messages[0];
          this.deleteError = true;
        }

        this.loadCategories();
      },
      error: () => {
        this.isDeleting = false;
        this.deleteMessage = 'Unable to delete. Please check your connection and try again.';
        this.deleteError = true;
      }
    });
  }

  private emptyForm(): CategoryFormRequest {
    return {
      categoryName: '',
      parentCategoryId: null
    };
  }
}

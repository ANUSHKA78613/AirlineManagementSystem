import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-image-upload',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="image-upload-wrapper">
      <div 
        class="upload-area" 
        [class.dragging]="isDragging"
        (dragover)="onDragOver($event)"
        (dragleave)="onDragLeave($event)"
        (drop)="onDrop($event)"
        (click)="fileInput.click()">
        
        <input 
          #fileInput
          type="file" 
          [accept]="acceptedTypes"
          style="display: none"
          (change)="onFileSelected($event)">
          
        @if (previewUrl) {
          <img [src]="previewUrl" class="preview-img" alt="Preview">
          <div class="overlay">
            <span>Click or drag to replace</span>
          </div>
        } @else {
          <div class="upload-placeholder">
            <span class="upload-icon">📷</span>
            <p>Click or drag image here</p>
            <small>Max size: 2MB</small>
          </div>
        }
      </div>
      
      @if (previewUrl && !autoUpload) {
        <div class="actions">
          <button type="button" class="btn btn-primary btn-sm" (click)="upload()">Upload</button>
          <button type="button" class="btn btn-secondary btn-sm" (click)="clear()">Clear</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .image-upload-wrapper { display: inline-block; }
    .upload-area {
      width: 120px;
      height: 120px;
      border: 2px dashed var(--border-color);
      border-radius: 50%;
      overflow: hidden;
      position: relative;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(255,255,255,0.05);
      transition: all 0.3s ease;
    }
    .upload-area.dragging, .upload-area:hover {
      border-color: var(--accent-primary);
      background: rgba(255,255,255,0.1);
    }
    .preview-img { width: 100%; height: 100%; object-fit: cover; }
    .upload-placeholder {
      text-align: center; color: var(--text-secondary); padding: 10px;
    }
    .upload-icon { font-size: 24px; display: block; margin-bottom: 4px; }
    .upload-placeholder p { font-size: 11px; margin: 0 0 2px; }
    .upload-placeholder small { font-size: 9px; opacity: 0.7; }
    .overlay {
      position: absolute; inset: 0; background: rgba(0,0,0,0.6);
      display: flex; align-items: center; justify-content: center;
      opacity: 0; transition: opacity 0.2s;
    }
    .overlay span { color: #fff; font-size: 11px; text-align: center; padding: 0 10px; }
    .upload-area:hover .overlay { opacity: 1; }
    .actions { display: flex; gap: 8px; justify-content: center; margin-top: 12px; }
  `]
})
export class ImageUploadComponent {
  @Input() existingImageUrl: string | null = null;
  @Input() acceptedTypes = 'image/jpeg, image/png, image/webp';
  @Input() autoUpload = false;
  
  @Output() fileSelected = new EventEmitter<File>();
  @Output() fileUploaded = new EventEmitter<File>(); // For manual upload

  previewUrl: string | ArrayBuffer | null = null;
  isDragging = false;
  selectedFile: File | null = null;
  
  ngOnInit() {
    if (this.existingImageUrl) {
      this.previewUrl = this.existingImageUrl;
    }
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['existingImageUrl'] && !this.selectedFile) {
      this.previewUrl = changes['existingImageUrl'].currentValue;
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragging = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragging = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragging = false;
    
    if (event.dataTransfer?.files.length) {
      this.handleFile(event.dataTransfer.files[0]);
    }
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.handleFile(input.files[0]);
    }
  }

  handleFile(file: File) {
    if (file.size > 2 * 1024 * 1024) {
      alert('File too large. Profile images are limited to 2MB to preserve database performance.');
      return;
    }
    
    if (!file.type.match('image.*')) {
      alert('Only images are allowed.');
      return;
    }
    
    this.selectedFile = file;
    
    // Create preview
    const reader = new FileReader();
    reader.onload = (e) => this.previewUrl = e.target?.result || null;
    reader.readAsDataURL(file);
    
    this.fileSelected.emit(file);
    
    if (this.autoUpload) {
      this.fileUploaded.emit(file);
    }
  }

  upload() {
    if (this.selectedFile) {
      this.fileUploaded.emit(this.selectedFile);
    }
  }

  clear() {
    this.selectedFile = null;
    this.previewUrl = this.existingImageUrl;
    this.fileSelected.emit(undefined);
  }
}
